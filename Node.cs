using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Node : IDisposable
{
	[Flags]
	public enum Condition
	{
		Available = 1,
		Unavailable = 2,
		Destroying = 4,
		Destroyed = 8,
		Disposed = Destroying | Destroyed
	}

	public delegate void NodeHandler(Node node);
	public delegate void NodeParentChildHandler(Node parent, Node child, int index);
	public delegate void NodeNewParentChildHandler(Node newParent, Node oldParent, Node child);
	public delegate void NodeTreeStructureHandler(Node affected, Node source, Node newParent, Node oldParent);
	public delegate void NodeConditionHandler(Node node, Condition newCondition, Condition previousCondition);

	// Parenting
	public event NodeHandler NodeStartedDisposeEvent;
	public event NodeHandler NodeDisposedEvent;
	public event NodeNewParentChildHandler NewParentSetEvent;
	public event NodeTreeStructureHandler TreeStructureChangedEvent;
	public event NodeParentChildHandler ChildAddedEvent;
	public event NodeParentChildHandler ChildRemovedEvent;

	// Condition Change
	public event NodeConditionHandler NodeConditionChangedEvent;

	public string NodeId
	{
		get;
	}

	public Node Parent
	{
		get; private set;
	}

	public Condition NodeCondition
	{
		get; private set;
	}

	private List<Node> _children = new List<Node>();
	private Dictionary<string, Node> _nodeIdToChildMap = new Dictionary<string, Node>();
	private NodeReferences _queuedNodes = new NodeReferences();

	public Node(string nodeId)
	{
		NodeId = nodeId;

		if(!CanUseNodeId(NodeId, out string validationError))
		{
			NodeId = Guid.NewGuid().ToString();
			if(!string.IsNullOrEmpty(nodeId))
			{
				Debug.LogError($"NodeId '{nodeId}' can't be used: '{validationError}. New Id == '{NodeId}'");
			}
		}

		NodeCondition = Condition.Available;
	}

	public bool TryGetRelativePathTo(Node targetNode, out string path)
	{
		// Is it me?
		if(this == targetNode)
		{
			path = string.Empty;
			return true;
		}
		// It is above me
		else if(IsAnyOfParentNodes(targetNode, out path))
		{
			return true;
		}
		// It is under me
		else if(targetNode.IsAnyOfParentNodes(this))
		{
			StringBuilder pathBuilder = new StringBuilder();
			Node checkingNode = targetNode;
			while(true)
			{
				Node nextNode = checkingNode.Parent;
				if(nextNode != this)
				{
					pathBuilder.Insert(0, string.Concat("/", checkingNode.NodeId));
					checkingNode = nextNode;
				}
				else
				{
					pathBuilder.Insert(0, checkingNode.NodeId);
					path = pathBuilder.ToString();
					return true;
				}
			}
		}
		// We share a parent
		else if(TryGetCommonParentNode(targetNode, out Node commonParent, out path))
		{
			string parentFullPath = commonParent.GetFullNodePath();
			string targetFullPath = targetNode.GetFullNodePath();
			path = path + targetFullPath.Substring(targetFullPath.IndexOf('/') >= 0 && parentFullPath.Length > 0 ? parentFullPath.Length + 1 : 0);
			return true;
		}
		// Can't find any link..
		else
		{
			path = string.Empty;
			return false;
		}
	}

	public bool TryGetCommonParentNode(Node otherNode, out Node commonParent)
	{
		return TryGetCommonParentNode(otherNode, out commonParent, out _);
	}

	public bool TryGetCommonParentNode(Node otherNode, out Node commonParent, out string path)
	{
		StringBuilder pathBuilder = new StringBuilder();
		Node checkingNode = Parent;
		while(checkingNode != null)
		{
			pathBuilder.Append("../");
			if(otherNode.IsAnyOfParentNodes(checkingNode))
			{
				commonParent = checkingNode;
				path = pathBuilder.ToString();
				return true;
			}
			checkingNode = checkingNode.Parent;
		}

		commonParent = null;
		path = string.Empty;
		return false;
	}

	public bool IsAnyOfParentNodes(Node potentialParent)
	{
		return IsAnyOfParentNodes(potentialParent, out _);
	}

	public bool IsAnyOfParentNodes(Node potentialParent, out string path)
	{
		StringBuilder stringBuilder = new StringBuilder();
		Node checkingNode = Parent;
		while(checkingNode != null)
		{
			stringBuilder.Append("../");
			if(checkingNode == potentialParent)
			{
				path = stringBuilder.ToString();
				return true;
			}
			checkingNode = checkingNode.Parent;
		}
		path = string.Empty;
		return false;
	}

	public Node[] GetChildren()
	{
		return _children.ToArray();
	}

	public Node[] GetChildren(Predicate<Node> predicate)
	{
		List<Node> values = new List<Node>();
		for(int i = 0, c = _children.Count; i < c; i++)
		{
			Node child = _children[i];
			if(predicate(child))
			{
				values.Add(child);
			}
		}
		return values.ToArray();
	}

	public bool IsInConditions(Condition conditions)
	{
		return conditions.HasFlag(NodeCondition);
	}

	public int GetNodeIndex(Node node)
	{
		return _children.IndexOf(node);
	}

	public bool SetNodeIndex(Node node, int index)
	{
		int oldIndex = GetNodeIndex(node);
		try
		{
			if(oldIndex >= 0)
			{
				_children.Remove(node);
				_children.Insert(index, node);
				return true;
			}
			else
			{
				throw new Exception("Node not a child");
			}
		}
		catch
		{
			if(!_children.Contains(node) && oldIndex >= 0)
			{
				_children.Insert(oldIndex, node);
			}
			return false;
		}
	}

	public bool SwapNodes(Node childA, Node childB)
	{
		return SwapNodes(GetNodeIndex(childA), GetNodeIndex(childB));
	}

	public bool SwapNodes(Node childA, int indexChildB)
	{
		return SwapNodes(GetNodeIndex(childA), indexChildB);
	}

	public bool SwapNodes(int indexChildA, int indexChildB)
	{
		if(IsChildIndexInRange(indexChildA) && IsChildIndexInRange(indexChildB))
		{
			Node nodeIndexB = _children[indexChildB];
			_children[indexChildB] = _children[indexChildA];
			_children[indexChildA] = nodeIndexB;
			return true;
		}
		return false;
	}

	public bool IsChildIndexInRange(int index)
	{
		return index >= 0 && index < _children.Count - 1;
	}

	public NodeT[] GetChildren<NodeT>()
	{
		List<NodeT> values = new List<NodeT>();
		for(int i = 0, c = _children.Count; i < c; i++)
		{
			Node child = _children[i];
			if(child is NodeT castedChild)
			{
				values.Add(castedChild);
			}
		}
		return values.ToArray();
	}

	public NodeT[] GetChildren<NodeT>(Predicate<NodeT> predicate)
	{
		List<NodeT> values = new List<NodeT>();
		for(int i = 0, c = _children.Count; i < c; i++)
		{
			if(_children[i] is NodeT castedChild && predicate(castedChild))
			{
				values.Add(castedChild);
			}
		}
		return values.ToArray();
	}

	public bool HasNode(Node node)
	{
		return FindNode(x => x == node) != null;
	}

	public Node FindNode(Predicate<Node> predicate, bool invertSearch = false)
	{
		return FindNode<Node>(predicate, invertSearch);
	}

	public Node FindNode(bool invertSearch = false)
	{
		if(_children.Count == 0)
		{
			return null;
		}

		return _children[invertSearch ? _children.Count - 1 : 0];
	}

	public Node GetNode(Enum enumValue)
	{
		return GetNode(enumValue.ToString());
	}

	public NodeT GetNode<NodeT>(Enum enumValue) where NodeT : Node
	{
		return GetNode<NodeT>(enumValue.ToString());
	}

	public Node GetNode(string path)
	{
		return GetNode<Node>(path);
	}

	public bool HasNode(string path)
	{
		return GetNode(path) != null;
	}

	public NodeT FindNode<NodeT>(bool invertSearch = false) where NodeT : Node
	{
		for(int i = 0, c = _children.Count; i < c; i++)
		{
			int index = invertSearch ? _children.Count - (i + 1) : i;
			Node node = _children[index];
			if(node is NodeT castedChild)
			{
				return castedChild;
			}
		}
		return null;
	}

	public NodeT FindNode<NodeT>(Predicate<NodeT> predicate, bool invertSearch = false) where NodeT : Node
	{
		for(int i = 0, c = _children.Count; i < c; i++)
		{
			int index = invertSearch ? _children.Count - (i + 1) : i;
			Node node = _children[index];
			if(node is NodeT castedChild && predicate(castedChild))
			{
				return castedChild;
			}
		}
		return null;
	}

	public NodeT GetNode<NodeT>(string path) where NodeT : Node
	{
		Queue<string> pathQueue = new Queue<string>(path.Split('/'));
		Node node = this;
		while(pathQueue.Count > 0)
		{
			string nodeId = pathQueue.Dequeue();
			if(nodeId == "..")
			{
				node = node.Parent;
			}
			else
			{
				node = node.GetLocalNode(nodeId);
			}

			if(node == null)
			{
				return null;
			}
		}
		return node as NodeT;
	}

	public bool HasNode<NodeT>(string path) where NodeT : Node
	{
		return GetNode<NodeT>(path) != null;
	}

	public bool HasNode<NodeT>(Enum enumValue) where NodeT : Node
	{
		return GetNode<NodeT>(enumValue) != null;
	}

	public Node GetLocalNode(string nodeId)
	{
		if(string.IsNullOrEmpty(nodeId))
		{
			return this;
		}

		if(_nodeIdToChildMap.TryGetValue(nodeId, out Node node))
		{
			return node;
		}
		return null;
	}

	public NodeT GetLocalNode<NodeT>(string nodeId) where NodeT : Node
	{
		return GetLocalNode(nodeId) as NodeT;
	}

	public Node GetRoot()
	{
		Node checkingNode = this;
		while(checkingNode != null)
		{
			if(checkingNode.Parent == null)
			{
				return checkingNode;
			}
			checkingNode = checkingNode.Parent;
		}
		return checkingNode;
	}

	public void SetParent(Node parentNode)
	{
		if(parentNode != null)
		{
			parentNode.AddChild(this);
		}
		else if(Parent != null)
		{
			Parent.RemoveChild(this);
		}
	}

	public void QueueChild(Node node)
	{
		if(CanAddChild(node, out _))
		{
			AddChild(node);
		}
		else
		{
			_queuedNodes.AddReference(node);
		}
	}

	public void AddChild(Node node)
	{
		if(CanAddChild(node, out string validationError))
		{
			int index = _children.Count;
			node.SetParent(null);
			_children.Add(node);
			_nodeIdToChildMap.Add(node.NodeId, node);
			node._UpdateParent(this);
			OnChildAdded(node);
			ChildAddedEvent?.Invoke(this, node, index);
		}
		else
		{
			Debug.LogError(validationError);
		}
	}

	public void AddChildren(Node[] nodes)
	{
		if(nodes == null)
		{
			return;
		}

		for(int i = 0, c = nodes.Length; i < c; i++)
		{
			AddChild(nodes[i]);
		}
	}

	public void RemoveChild(string nodeId)
	{
		if(CanRemoveChild(nodeId, out string validationError) && _nodeIdToChildMap.TryGetValue(nodeId, out Node node))
		{
			RemoveChild(node);
		}
		else
		{
			Debug.LogError(validationError);
		}
	}

	public void RemoveChild(Node node)
	{
		if(CanRemoveChild(node.NodeId, out string validationError))
		{
			int index = GetNodeIndex(node);
			_nodeIdToChildMap.Remove(node.NodeId);
			_children.Remove(node);
			node._UpdateParent(null);
			OnChildRemoved(node);
			ChildRemovedEvent?.Invoke(this, node, index);

			TryAddingQueuedNodes();
		}
		else
		{
			Debug.LogError(validationError);
		}
	}

	public string GetFullNodePath()
	{
		StringBuilder pathBuilder = new StringBuilder("");
		Node current = this;
		while(true)
		{
			pathBuilder.Insert(0, current.NodeId);
			current = current.Parent;

			// Include all parents, exclude Root node
			if(current != null && current.Parent != null)
			{
				pathBuilder.Insert(0, "/");
			}
			else
			{
				break;
			}
		}
		return pathBuilder.ToString();
	}

	public void SetAvailable(bool available)
	{
		SetCondition(available ? Condition.Available : Condition.Unavailable);
	}

	public void Dispose()
	{
		if(SetCondition(Condition.Destroying))
		{
			OnStartedDispose();
			NodeStartedDisposeEvent?.Invoke(this);

			// Disconnect From Parent
			SetParent(null);

			// Clean-up recursively (which causes each child to disconnect from their parent and clean-up)
			_queuedNodes.Dispose();

			Node[] children = GetChildren();
			for(int i = children.Length - 1; i >= 0; i--)
			{
				children[i].Dispose();
			}

			SetCondition(Condition.Destroyed);
			NodeDisposedEvent?.Invoke(this);

			_queuedNodes = null;

			ChildAddedEvent = null;
			ChildRemovedEvent = null;
			NewParentSetEvent = null;
			TreeStructureChangedEvent = null;
			NodeConditionChangedEvent = null;
			NodeStartedDisposeEvent = null;
			NodeDisposedEvent = null;
		}
	}

	public bool CanSetCondition(Condition newCondition, out string validationError)
	{
		if(NodeCondition == newCondition)
		{
			validationError = $"Already in condition {newCondition}";
			return false;
		}

		switch(NodeCondition)
		{
			case Condition.Available:
				if(newCondition != Condition.Unavailable && newCondition != Condition.Destroying)
				{
					validationError = $"Can't switch to condition {newCondition} while in condition {NodeCondition}";
					return false;
				}
				validationError = string.Empty;
				return true;
			case Condition.Unavailable:
				if(newCondition != Condition.Available && newCondition != Condition.Destroying)
				{
					validationError = $"Can't switch to condition {newCondition} while in condition {NodeCondition}";
					return false;
				}
				validationError = string.Empty;
				return true;
			case Condition.Destroying:
				if(newCondition != Condition.Destroyed)
				{
					validationError = "Can't Change condition for node is Destroying";
					return false;
				}
				validationError = string.Empty;
				return true;
			case Condition.Destroyed:
				validationError = "Can't Change condition for node is Destroyed";
				return false;
			default:
				validationError = string.Empty;
				return true;
		}
	}

	public bool CanRemoveChild(string nodeId, out string validationError)
	{
		if(string.IsNullOrEmpty(nodeId) || !_nodeIdToChildMap.ContainsKey(nodeId))
		{
			validationError = $"Can't remove node with nodeId '{nodeId}' for it is not a child of {this}";
			return false;
		}

		validationError = string.Empty;
		return true;
	}

	public bool CanUseNodeId(string nodeId, out string validationError)
	{
		if(string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrEmpty(nodeId))
		{
			validationError = "NodeId can't be null, empty or white space";
			return false;
		}

		if(nodeId.IndexOf('/') >= 0 || NodeId.IndexOf('.') >= 0)
		{
			validationError = "NodeId can't contain '/' or '.'";
			return false;
		}

		validationError = string.Empty;
		return true;
	}

	public bool CanAddChild(Node node, out string validationError)
	{
		if(node == null)
		{
			validationError = $"Can't add null node";
			return false;
		}

		if(node == this)
		{
			validationError = $"Can't add {node} to itself";
			return false;
		}

		if(_nodeIdToChildMap.ContainsKey(node.NodeId))
		{
			validationError = $"Already containing child with id {node.NodeId}";
			return false;
		}

		if(NodeCondition != Condition.Available)
		{
			validationError = $"{node} is no longer available so no new children can be added";
			return false;
		}

		Node n = Parent;
		while(n != null)
		{
			if(n == node)
			{
				validationError = $"Can't add {node} for {this} is under its children structure";
				return false;
			}
			n = n.Parent;
		}

		validationError = string.Empty;
		return true;
	}

	public virtual string GetNodeDebugInfo(out string details)
	{
		details = string.Empty;
		return "-"; 
	}

	public override string ToString()
	{
		return string.Format("Node ID: {0}", NodeId);
	}

	protected virtual void OnEnter()
	{

	}

	protected virtual void OnExit()
	{

	}

	protected virtual void OnStartedDispose()
	{

	}

	protected virtual void OnChildAdded(Node child)
	{

	}

	protected virtual void OnChildRemoved(Node child)
	{

	}

	protected virtual void OnParentChanged(Node newParent, Node previousParent)
	{

	}

	protected virtual void OnNodeConditionChanged(Condition newCondition, Condition previousCondition)
	{

	}

	protected virtual void OnTreeStructureChanged(Node node, Node newParent, Node oldParent)
	{

	}

	private void TryAddingQueuedNodes()
	{
		Node[] queuedNodes = _queuedNodes?.GetReferences(NodeReferences.AvailabilityType.All) ?? new Node[] { };

		for(int i = queuedNodes.Length - 1; i >= 0; i--)
		{
			Node node = queuedNodes[i];
			if(CanAddChild(node, out _))
			{
				AddChild(node);
				_queuedNodes.RemoveReference(node);
			}
		}
	}

	private bool SetCondition(Condition condition)
	{
		if(NodeCondition != condition)
		{
			if(CanSetCondition(condition, out string validationError))
			{
				Condition previousCondition = NodeCondition;
				NodeCondition = condition;
				OnNodeConditionChanged(NodeCondition, previousCondition);
				NodeConditionChangedEvent?.Invoke(this, NodeCondition, previousCondition);
				return true;
			}
			else
			{
				Debug.LogError(validationError);
				return false;
			}
		}
		return false;
	}

	private void NotifyTreeStructureChanged(Node node, Node newParent, Node oldParent)
	{
		OnTreeStructureChanged(node, newParent, oldParent);
		for(int i = _children.Count - 1; i >= 0; i--)
		{
			_children[i].NotifyTreeStructureChanged(node, newParent, oldParent);
		}
		TreeStructureChangedEvent?.Invoke(this, node, newParent, oldParent);
	}

	internal void _UpdateParent(Node parent)
	{
		if(Parent != parent)
		{
			Node previousParent = Parent;
			Parent = parent;

			if(previousParent != null)
			{
				OnExit();
			}

			if(Parent != null)
			{
				OnEnter();
			}

			OnParentChanged(Parent, previousParent);
			NewParentSetEvent?.Invoke(Parent, previousParent, this);
			NotifyTreeStructureChanged(this, Parent, previousParent);
		}
	}
}
