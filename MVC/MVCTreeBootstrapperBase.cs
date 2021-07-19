using System;

public abstract class MVCTreeBootstrapperBase<EnumT> : IDisposable
	where EnumT : unmanaged, Enum
{
	public PointerNodeRef<Node>[] LayerRefs
	{
		get; private set;
	}

	public MVCTreeBootstrapperBase()
	{
		EnumT[] layers = Enum.GetValues(typeof(EnumT)) as EnumT[];
		LayerRefs = new PointerNodeRef<Node>[layers.Length];
		for(int i = 0, c = layers.Length; i < c; i++)
		{
			LayerRefs[i] = new PointerNodeRef<Node>(CreateLayerRoot(layers[i]));
		}
	}

	public Node GetLayerRootNode(EnumT layer)
	{
		return LayerRefs[Array.IndexOf(Enum.GetValues(typeof(EnumT)), layer)]?.Instance;
	}

	public NodeT GetLayerRootNode<NodeT>(EnumT layer) where NodeT : Node
	{
		return LayerRefs[Array.IndexOf(Enum.GetValues(typeof(EnumT)), layer)]?.Instance as NodeT;
	}

	public void Dispose()
	{
		for(int i = LayerRefs.Length - 1; i >= 0; i--)
		{
			LayerRefs[i]?.Instance?.Dispose();
		}
	}

	protected abstract Node CreateLayerRoot(EnumT layer);
}