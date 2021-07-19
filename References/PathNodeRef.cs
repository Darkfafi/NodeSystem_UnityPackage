public class PathNodeRef<NodeT> : INodeRef<NodeT> where NodeT : Node
{
	public event NodeRefGlobals<NodeT>.InstanceHandler InstanceChangedEvent;
	public event NodeRefGlobals<NodeT>.VoidHandler OnDisposalEvent;

	public bool HasInstance => Instance != null;

	public NodeT Instance
	{
		get; private set;
	}

	public Node PathOrigin
	{
		get; private set;
	}

	private string _relativePath;

	public PathNodeRef(Node pathOrigin, Node targetNode, NodeRefGlobals<NodeT>.InstanceHandler onInstanceChanged = null, NodeRefGlobals<NodeT>.VoidHandler onDispose = null)
	{
		InstanceChangedEvent = onInstanceChanged;
		OnDisposalEvent = onDispose;

		PathOrigin = pathOrigin;
		PathOrigin.NodeConditionChangedEvent += OnContainerConditionChangedEvent;
		PathOrigin.TreeStructureChangedEvent += OnTreeStructureChangedEvent;
		if(pathOrigin.TryGetRelativePathTo(targetNode, out string path))
		{
			_relativePath = path;
			SetInstance(pathOrigin.GetNode<NodeT>(_relativePath));
		}
		else
		{
			_relativePath = null;
			SetInstance(null);
		}
	}

	public PathNodeRef(Node pathOrigin, string relativePath, NodeRefGlobals<NodeT>.InstanceHandler onInstanceChanged = null, NodeRefGlobals<NodeT>.VoidHandler onDispose = null)
	{
		InstanceChangedEvent = onInstanceChanged;
		OnDisposalEvent = onDispose;

		PathOrigin = pathOrigin;
		PathOrigin.NodeConditionChangedEvent += OnContainerConditionChangedEvent;
		PathOrigin.TreeStructureChangedEvent += OnTreeStructureChangedEvent;
		_relativePath = relativePath;
		SetInstance(pathOrigin.GetNode<NodeT>(relativePath));
	}

	public bool TryGetInstance(out NodeT instance)
	{
		instance = Instance;
		return HasInstance;
	}

	public void Dispose()
	{
		OnDisposalEvent?.Invoke();

		SetInstance(null);

		PathOrigin.NodeConditionChangedEvent -= OnContainerConditionChangedEvent;
		PathOrigin.TreeStructureChangedEvent -= OnTreeStructureChangedEvent;

		PathOrigin = null;
		_relativePath = null;

		InstanceChangedEvent = null;
		OnDisposalEvent = null;
	}

	private void OnTreeStructureChangedEvent(Node affected, Node source, Node newParent, Node oldParent)
	{
		if(_relativePath != null)
		{
			SetInstance(affected.GetNode<NodeT>(_relativePath));
		}
	}

	private void SetInstance(NodeT instance)
	{
		NodeT oldInstance = Instance;

		if(Instance != null)
		{
			Instance.NewParentSetEvent -= OnNewParentSetEvent;
			Instance = null;
		}
		else
		{
			PathOrigin.ChildAddedEvent -= OnContainerChildAddedEvent;
		}

		Instance = instance;

		if(Instance != null)
		{
			Instance.NewParentSetEvent += OnNewParentSetEvent;
		}
		else
		{
			PathOrigin.ChildAddedEvent += OnContainerChildAddedEvent;
		}

		if(Instance != oldInstance)
		{
			InstanceChangedEvent?.Invoke(Instance, oldInstance);
		}
	}

	private void OnContainerChildAddedEvent(Node parent, Node child, int index)
	{
		if(child.NodeId == _relativePath && child is NodeT castedChild)
		{
			SetInstance(castedChild);
		}
	}

	private void OnContainerConditionChangedEvent(Node node, Node.Condition newCondition, Node.Condition previousCondition)
	{
		if(PathOrigin == node && newCondition == Node.Condition.Destroying)
		{
			Dispose();
		}
	}

	private void OnNewParentSetEvent(Node newParent, Node oldParent, Node child)
	{
		if(newParent != PathOrigin && Instance == child)
		{
			SetInstance(null);
		}
	}
}
