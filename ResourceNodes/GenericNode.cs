using System;

public class GenericNode<DataT> : Node, IReadOnlyGenericNode<DataT>
{
	public delegate void ValueHandler(DataT newValue, DataT oldValue);
	public event ValueHandler ValueChangedEvent;
	public event ValueHandler PreValueChangedEvent;
	public event Action<DataT> SetValueEvent;

	public DataT Value
	{
		get; private set;
	}

	public GenericNode(string nodeId, DataT defaultValue)
		: base(nodeId)
	{
		Value = defaultValue;
	}

	public void SetValue(DataT val)
	{
		if(!Value.Equals(val))
		{
			PreValueChangedEvent?.Invoke(val, Value);
			DataT oldValue = Value;
			Value = val;
			ValueChangedEvent?.Invoke(Value, oldValue);
		}
		SetValueEvent?.Invoke(Value);
	}

	public override string GetNodeDebugInfo(out string details)
	{
		details = Value.GetType().Name;
		return Value.ToString();
	}

	protected override void OnStartedDispose()
	{
		PreValueChangedEvent = null;
		ValueChangedEvent = null;
		SetValueEvent = null;
		base.OnStartedDispose();
	}
}

public interface IReadOnlyGenericNode<DataT>
{
	event GenericNode<DataT>.ValueHandler ValueChangedEvent;

	DataT Value
	{
		get;
	}
}