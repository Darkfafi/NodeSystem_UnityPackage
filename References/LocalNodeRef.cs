public class LocalNodeRef<NodeT> : INodeRef<NodeT> where NodeT : Node
{
	public event NodeRefGlobals<NodeT>.InstanceHandler InstanceChangedEvent;
	public event NodeRefGlobals<NodeT>.VoidHandler OnDisposalEvent; 

	public readonly string RefNodeId;
	private Node _container;

	public bool HasInstance => Instance != null;

	public NodeT Instance
	{
		get; private set;
	}

	public LocalNodeRef(Node container, string nodeId, NodeRefGlobals<NodeT>.InstanceHandler onInstanceChanged = null, NodeRefGlobals<NodeT>.VoidHandler onDispose = null)
	{
		InstanceChangedEvent = onInstanceChanged;
		OnDisposalEvent = onDispose;

		_container = container;
		_container.NodeConditionChangedEvent += OnContainerConditionChangedEvent;
		RefNodeId = nodeId;
		SetInstance(container.GetLocalNode<NodeT>(nodeId));
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

		_container.NodeConditionChangedEvent -= OnContainerConditionChangedEvent;
		_container = null;

		InstanceChangedEvent = null;
		OnDisposalEvent = null;
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
			_container.ChildAddedEvent -= OnContainerChildAddedEvent;
		}

		Instance = instance;

		if(Instance != null)
		{
			Instance.NewParentSetEvent += OnNewParentSetEvent;
		}
		else
		{
			_container.ChildAddedEvent += OnContainerChildAddedEvent;
		}

		if(Instance != oldInstance)
		{
			InstanceChangedEvent?.Invoke(Instance, oldInstance);
		}
	}

	private void OnContainerChildAddedEvent(Node parent, Node child, int index)
	{
		if(child.NodeId == RefNodeId && child is NodeT castedChild)
		{
			SetInstance(castedChild);
		}
	}

	private void OnContainerConditionChangedEvent(Node node, Node.Condition newCondition, Node.Condition previousCondition)
	{
		if(_container == node && newCondition == Node.Condition.Destroying)
		{
			Dispose();
		}
	}

	private void OnNewParentSetEvent(Node newParent, Node oldParent, Node child)
	{
		if(newParent != _container && Instance == child)
		{
			SetInstance(null);
		}
	}
}
