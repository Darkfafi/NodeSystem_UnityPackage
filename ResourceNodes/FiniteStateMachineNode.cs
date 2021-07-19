using System;
using UnityEngine;

public class FiniteStateMachineNode : FiniteStateMachineNode<FiniteStateNode>
{
	public FiniteStateMachineNode(string nodeId, FiniteStateNode[] states, int defaultStateIndex = 0) 
		: base(nodeId, states, defaultStateIndex)
	{

	}
}

public class FiniteStateMachineNode<FiniteStateNodeT> : Node where FiniteStateNodeT : FiniteStateNode
{
	public delegate void StateHandler(FiniteStateNodeT newState, FiniteStateNodeT oldState);
	public event StateHandler StateChangingEvent;
	public event StateHandler StateChangedEvent;

	public FiniteStateNodeT CurrentStateNode
	{
		get; private set;
	}

	public IReadOnlyGenericNode<int> CurrentStateNodeIndex => _currentStatePointerNode.Instance;
	public readonly LocalNodeRef<Node> StatesHolder;

	private LocalNodeRef<GenericNode<int>> _currentStatePointerNode;

	public FiniteStateMachineNode(string nodeId, FiniteStateNodeT[] states, int defaultStateIndex = 0)
		: base(nodeId)
	{
		_currentStatePointerNode = new LocalNodeRef<GenericNode<int>>(this, nameof(_currentStatePointerNode), (newValue, oldValue) => 
		{
			if(oldValue != null)
			{
				oldValue.SetValueEvent -= OnStatePointerSetValueEvent;
			}

			if(newValue != null)
			{
				newValue.SetValueEvent += OnStatePointerSetValueEvent;
			}
		});

		StatesHolder = new LocalNodeRef<Node>(this, nameof(StatesHolder), (newValue, oldValue) => 
		{
			if(oldValue != null)
			{
				oldValue.ChildAddedEvent -= OnChildAddedToStates;
				oldValue.ChildRemovedEvent -= OnChildRemovedFromStates;
			}

			if(newValue != null)
			{
				newValue.ChildRemovedEvent += OnChildRemovedFromStates;
				newValue.ChildAddedEvent += OnChildAddedToStates;
			}
		});

		AddChild(new Node(nameof(StatesHolder)));
		AddChild(new GenericNode<int>(nameof(_currentStatePointerNode), int.MinValue));

		for(int i = 0, c = states.Length; i < c; i++)
		{
			StatesHolder.Instance.AddChild(states[i]);
		}
		_currentStatePointerNode.Instance.SetValue(defaultStateIndex);
	}

	public override string GetNodeDebugInfo(out string details)
	{
		if(CurrentStateNode != null)
		{
			details = CurrentStateNode.GetNodeDebugInfo(out _);
			return "Current: " + CurrentStateNode.NodeId;
		}
		else
		{
			details = string.Empty;
			return "Current: N/A";
		}
	}

	public FiniteStateNodeT[] GetAllStates()
	{
		if(StatesHolder.HasInstance)
		{
			return StatesHolder.Instance.GetChildren<FiniteStateNodeT>();
		}
		return new FiniteStateNodeT[] { };
	}

	public int GetAllStatesCount()
	{
		return GetAllStates().Length;
	}

	public void AddState(FiniteStateNodeT finiteStateNode)
	{
		StatesHolder.Instance.AddChild(finiteStateNode);
	}

	public void ProgressStateMachine(int? startMargin = null, int? endMargin = null)
	{
		int statesCount = GetAllStatesCount();
		if(startMargin.HasValue)
		{
			startMargin = Mathf.Clamp(startMargin.Value, 0, statesCount - 1);
		}
		else
		{
			startMargin = 0;
		}

		if(endMargin.HasValue)
		{
			endMargin = Mathf.Clamp(statesCount + endMargin.Value, 0, statesCount - 1);
		}
		else
		{
			endMargin = statesCount - 1;
		}

		int nextIndex = NodeSystemUtils.CycleIndex(CurrentStateNode == null ? 0 : GetStateIndex(CurrentStateNode) + 1, statesCount);

		if(nextIndex < startMargin.Value || nextIndex > endMargin.Value)
		{
			nextIndex = startMargin.Value;
		}

		SetState(nextIndex, true);
	}

	public FiniteStateNodeT GetState(int index, bool cycling = true)
	{
		FiniteStateNodeT[] states = GetAllStates();

		if(states.Length == 0)
		{
			return null;
		}

		if(cycling)
		{
			index = NodeSystemUtils.CycleIndex(index, states.Length);
		}

		if(index >= 0 && index < states.Length)
		{
			return states[index];
		}

		return null;
	}

	public void SetState(FiniteStateNodeT newState)
	{
		int index = GetStateIndex(newState);
		if(index >= 0)
		{
			SetState(index, false);
		}
		else
		{
			if(newState == null)
			{
				SetState(-1, false);
			}
			else if(StatesHolder.HasInstance)
			{
				newState.SetParent(StatesHolder.Instance);
				SetState(newState);
			}
			else
			{
				InternalSetState(newState);
			}
		}
	}

	public void SetState(string stateName)
	{
		SetState(StatesHolder.Instance?.GetNode<FiniteStateNodeT>(stateName));
	}

	public void SetState(Enum stateEnum)
	{
		SetState(StatesHolder.Instance?.GetNode<FiniteStateNodeT>(stateEnum));
	}

	public void SetState(int stateIndex, bool cycle)
	{
		if(cycle)
		{
			stateIndex = NodeSystemUtils.CycleIndex(stateIndex, GetAllStatesCount());
		}

		if(_currentStatePointerNode.HasInstance)
		{
			_currentStatePointerNode.Instance.SetValue(stateIndex);
		}
		else
		{
			InternalSetState(GetState(stateIndex, true));
		}
	}

	private void OnStatePointerSetValueEvent(int newStateIndex)
	{
		InternalSetState(newStateIndex);
	}

	private void InternalSetState(int stateIndex)
	{
		InternalSetState(GetState(stateIndex, false));
	}

	private void InternalSetState(FiniteStateNodeT state)
	{
		if(state != CurrentStateNode)
		{
			FiniteStateNodeT oldState = CurrentStateNode;

			if(CurrentStateNode != null)
			{
				CurrentStateNode._MarkAsLostCurrentState();
			}

			CurrentStateNode = state;

			StateChangingEvent?.Invoke(CurrentStateNode, oldState);

			if(CurrentStateNode != null)
			{
				CurrentStateNode._MarkAsCurrentState();
			}

			StateChangedEvent?.Invoke(CurrentStateNode, oldState);
		}
	}

	private void OnChildAddedToStates(Node parent, Node child, int index)
	{
		if(child is FiniteStateNodeT finiteStateNode)
		{
			finiteStateNode.SetAsCurrentStateRequestEvent += OnSetAsCurrentStateRequestEvent;
			CorrectContent();
		}
	}

	private void OnChildRemovedFromStates(Node parent, Node child, int index)
	{
		if(child is FiniteStateNodeT finiteStateNode)
		{
			finiteStateNode.SetAsCurrentStateRequestEvent -= OnSetAsCurrentStateRequestEvent;

			if(CurrentStateNode == finiteStateNode)
			{
				SetState(_currentStatePointerNode.HasInstance ? _currentStatePointerNode.Instance.Value : index, true);
			}
			else
			{
				CorrectContent();
			}
		}
	}

	private void CorrectContent()
	{
		if(_currentStatePointerNode.HasInstance && CurrentStateNode != null)
		{
			int currentStateIndex = GetStateIndex(CurrentStateNode);
			if(currentStateIndex >= 0 && currentStateIndex != _currentStatePointerNode.Instance.Value)
			{
				_currentStatePointerNode.Instance.SetValue(currentStateIndex);
			}
		}
	}

	private int GetStateIndex(FiniteStateNodeT finiteStateNode)
	{
		return Array.IndexOf(GetAllStates(), finiteStateNode);
	}

	private void OnSetAsCurrentStateRequestEvent(FiniteStateNode stateNode)
	{
		if(stateNode is FiniteStateNodeT finiteStateNode)
		{
			InternalSetState(finiteStateNode);
		}
	}
}

public class FiniteStateNode : Node
{
	public event Action<FiniteStateNode> EnteredState;
	public event Action<FiniteStateNode> ExitedState;

	public event Action<FiniteStateNode> SetAsCurrentStateRequestEvent;

	public bool IsActiveState
	{
		get; private set;
	}


	public FiniteStateNode(string nodeId) : base(nodeId)
	{

	}

	public void SetAsCurrentState()
	{
		SetAsCurrentStateRequestEvent?.Invoke(this);
	}

	protected virtual void OnStateEnter()
	{

	}

	protected virtual void OnStateExit()
	{

	}

	protected override void OnStartedDispose()
	{
		EnteredState = null;
		ExitedState = null;
		SetAsCurrentStateRequestEvent = null;
		base.OnStartedDispose();
	}

	internal void _MarkAsCurrentState()
	{
		IsActiveState = true;
		OnStateEnter();
		EnteredState?.Invoke(this);
	}

	internal void _MarkAsLostCurrentState()
	{
		OnStateExit();
		IsActiveState = false;
		ExitedState?.Invoke(this);
	}
}