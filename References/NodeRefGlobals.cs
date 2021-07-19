public static class NodeRefGlobals<NodeT> where NodeT : Node
{
	public delegate void InstanceHandler(NodeT newInstance, NodeT oldInstance);
	public delegate void VoidHandler();
}
