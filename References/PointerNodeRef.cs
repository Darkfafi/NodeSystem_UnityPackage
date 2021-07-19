public class PointerNodeRef<NodeT> : INodeRef<NodeT> where NodeT : Node
{
	public event NodeRefGlobals<NodeT>.InstanceHandler InstanceChangedEvent;
	public event NodeRefGlobals<NodeT>.VoidHandler OnDisposalEvent;

	public bool HasInstance => Instance != null;

	public NodeT Instance
	{
		get; private set;
	}

	public PointerNodeRef(NodeT nodeToPoint, NodeRefGlobals<NodeT>.InstanceHandler onInstanceChanged = null, NodeRefGlobals<NodeT>.VoidHandler onDispose = null)
	{
		InstanceChangedEvent = onInstanceChanged;
		OnDisposalEvent = onDispose;

		SetNewPointerTarget(nodeToPoint);
	}

	public bool TryGetInstance(out NodeT instance)
	{
		instance = Instance;
		return HasInstance;
	}

	public void SetNewPointerTarget(NodeT newTarget)
	{
		NodeT oldInstance = Instance;

		if(Instance != null)
		{
			Instance.NodeConditionChangedEvent -= OnNodeConditionChangedEvent;
		}

		Instance = newTarget;

		if(Instance != null)
		{
			Instance.NodeConditionChangedEvent += OnNodeConditionChangedEvent;
		}

		if(oldInstance != Instance)
		{
			InstanceChangedEvent?.Invoke(Instance, oldInstance);
		}
	}

	public void Dispose()
	{
		OnDisposalEvent?.Invoke();

		SetNewPointerTarget(null);

		OnDisposalEvent = null;
		InstanceChangedEvent = null;
	}

	private void OnNodeConditionChangedEvent(Node node, Node.Condition newCondition, Node.Condition previousCondition)
	{
		if(node == Instance && newCondition == Node.Condition.Destroying)
		{
			SetNewPointerTarget(null);
		}
	}
}
