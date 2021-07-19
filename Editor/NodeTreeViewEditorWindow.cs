using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class NodeTreeViewEditorWindow : EditorWindow
{
	private NodeTreeView _nodeTreeView;

	[MenuItem("NodeSystem/TreeView")]
	static void OpenWindow()
	{
		NodeTreeViewEditorWindow window = GetWindow<NodeTreeViewEditorWindow>();
		window.titleContent = new GUIContent("NodeTreeView");
		window.Show();
	}

	protected void Update()
	{
		if(_nodeTreeView != null && _nodeTreeView.HasTarget)
		{
			_nodeTreeView.Reload();
			Repaint();
		}
	}

	protected void OnEnable()
	{
		_nodeTreeView = new NodeTreeView(new TreeViewState());
	}

	protected void OnGUI()
	{
		if(_nodeTreeView != null)
		{
			GameObject activeObject = Selection.activeGameObject;

			IRootNodeHolder potentialTarget = activeObject != null ? activeObject.GetComponent<IRootNodeHolder>() : null;
			if(!_nodeTreeView.HasTarget || potentialTarget != _nodeTreeView.Target && potentialTarget != null)
			{
				_nodeTreeView.SetTarget(potentialTarget);
			}

			if(_nodeTreeView.Target != null && (!(_nodeTreeView.Target.RootNodeRef?.HasInstance ?? false) || _nodeTreeView.Target.RootNodeRef.Instance.IsInConditions(Node.Condition.Disposed)))
			{
				_nodeTreeView.SetTarget(null);
			}

			if(_nodeTreeView.HasTarget)
			{
				_nodeTreeView.OnGUI(new Rect(0, 0, position.width, position.height));
			}
			else
			{
				EditorGUILayout.LabelField($"No Active {nameof(IRootNodeHolder)} Selected");
			}
		}
	}

	private class NodeTreeView : TreeView
	{
		public bool HasTarget => Target?.RootNodeRef?.HasInstance ?? false;

		public IRootNodeHolder Target
		{
			get; private set;
		}

		private Dictionary<int, Node> _idToNode = new Dictionary<int, Node>();

		public NodeTreeView(TreeViewState state)
			: base(state, new MultiColumnHeader(new MultiColumnHeaderState(new MultiColumnHeaderState.Column[]
			{
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("NodeID"),
					autoResize = true,
					width = 100,
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("NodeType"),
					autoResize = true,
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("NodeCondition"),
					autoResize = true,
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("NodeInfo"),
					autoResize = true,
				}
			})))
		{
			rowHeight = 20;
			showAlternatingRowBackgrounds = true;
			showBorder = true;
			Reload();
		}

		public IReadOnlyDictionary<int, Node> IdToNode => _idToNode;

		public void SetTarget(IRootNodeHolder rootNodeHolder)
		{
			if(Target != rootNodeHolder)
			{
				Target = rootNodeHolder;
				multiColumnHeader.ResizeToFit();
				Reload();
			}
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			if(IdToNode.TryGetValue(args.item.id, out Node node))
			{
				for(int i = 0; i < args.GetNumVisibleColumns(); ++i)
				{
					Rect cellRect = args.GetCellRect(i);
					args.rowRect = cellRect;
					switch(i)
					{
						case 0:
							base.RowGUI(args);
							break;
						case 1:
							GUI.Label(cellRect, new GUIContent(node.GetType().Name, node.GetType().FullName));
							break;
						case 2:
							GUI.Label(cellRect, new GUIContent(node.NodeCondition.ToString()));
							break;
						case 3:
							GUI.Label(cellRect, new GUIContent(node.GetNodeDebugInfo(out string details), details));
							break;
					}
				}
			}
			else
			{
				base.RowGUI(args);
			}
		}

		public override void OnGUI(Rect rect)
		{
			base.OnGUI(rect);

			Event e = Event.current;
			if(e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && HasSelection())
			{
				foreach(var index in GetSelection())
				{
					if(IdToNode.TryGetValue(index, out Node nodeToDispose))
					{
						nodeToDispose.Dispose();
					}
				}

				if(!HasTarget)
				{
					SetTarget(null);
				}
			}
		}

		protected override TreeViewItem BuildRoot()
		{
			int id = 0;
			_idToNode.Clear();

			TreeViewItem root = CreateTreeViewItem(null);
			root.depth = -1;

			if(Target?.RootNodeRef?.HasInstance ?? false)
			{
				AddNode(root, Target.RootNodeRef.Instance);
			}

			if(root.children == null || root.children.Count == 0)
			{
				root.AddChild(CreateTreeViewItem(null, "N/A"));
			}

			SetupDepthsFromParentsAndChildren(root);

			return root;

			void AddNode(TreeViewItem parentTreeItem, Node node)
			{
				TreeViewItem nodeTreeItem = CreateTreeViewItem(node);
				_idToNode.Add(id, node);
				parentTreeItem.AddChild(nodeTreeItem);
				Node[] children = node.GetChildren();
				for(int i = 0; i < children.Length; i++)
				{
					AddNode(nodeTreeItem, children[i]);
				}
			}

			TreeViewItem CreateTreeViewItem(Node node, string fallbackName = "-")
			{
				return new TreeViewItem
				{
					id = ++id,
					displayName = node != null ? node.NodeId : fallbackName,
				};
			}
		}
	}
}
