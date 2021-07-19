using UnityEngine;

public class MVCRootNodeDisplay : MonoBehaviour, IRootNodeHolder
{
	public PointerNodeRef<Node> RootNodeRef
    {
        get; private set;
    }

    public void Init(Node rootNode)
    {
        RootNodeRef = new PointerNodeRef<Node>(rootNode);
        gameObject.name = rootNode.NodeId;
    }
}
