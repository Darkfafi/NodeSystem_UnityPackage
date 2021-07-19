using System;

public interface INodeRef<NodeT> : IDisposable where NodeT : Node
{
	event NodeRefGlobals<NodeT>.InstanceHandler InstanceChangedEvent;
	event NodeRefGlobals<NodeT>.VoidHandler OnDisposalEvent;

	NodeT Instance
	{
		get;
	}

	bool HasInstance
	{
		get;
	}

	bool TryGetInstance(out NodeT instance);
}