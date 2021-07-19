using NodeSystem.NodeReferencesInternal;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Node;
using static NodeReferences;

public class ReferencesNode : ReferencesNode<Node>
{
	public ReferencesNode(string nodeId) 
		: base(nodeId)
	{
	}
}

public class ReferencesNode<NodeT> : Node, INodeReferences<NodeT> where NodeT : Node
{
	public event ReferenceHandler<NodeT> ReferenceAddedEvent;
	public event ReferenceHandler<NodeT> ReferenceRemovedEvent;

	private NodeReferences<NodeT> _nodeReferences = new NodeReferences<NodeT>();

	public ReferencesNode(string nodeId) 
		: base(nodeId)
	{
		_nodeReferences.ReferenceAddedEvent += OnReferenceAddedEvent;
		_nodeReferences.ReferenceRemovedEvent += OnReferenceRemovedEvent;
	}

	public void AddChildrenReferencesListener(Node target)
	{
		_nodeReferences.AddChildrenReferencesListener(target);
	}

	public void AddReference(NodeT node)
	{
		_nodeReferences.AddReference(node);
	}

	public bool CanAddReference(NodeT node, out string validationError)
	{
		return _nodeReferences.CanAddReference(node, out validationError);
	}

	public bool CanRemoveReference(NodeT node, out string validationError)
	{
		return _nodeReferences.CanRemoveReference(node, out validationError);
	}

	public void ForEach(Action<NodeT> action, AvailabilityType availabilityType = AvailabilityType.Available)
	{
		_nodeReferences.ForEach(action, availabilityType);
	}

	public void ForEach(Action<NodeT> action, Predicate<NodeT> predicate, AvailabilityType availabilityType = AvailabilityType.Available)
	{
		_nodeReferences.ForEach(action, predicate, availabilityType);
	}

	public override string GetNodeDebugInfo(out string details)
	{
		string contentString = "";
		int i = 0;
		ForEach(x => contentString += string.Concat(i++, ": ", x.NodeId));
		details = contentString;
		return string.Concat("(", GetReferences( AvailabilityType.All).Length , ")", contentString);
	}

	public NodeT[] GetReferences(AvailabilityType availabilityType = AvailabilityType.Available)
	{
		return _nodeReferences.GetReferences(availabilityType);
	}

	public NodeT[] GetReferences(Predicate<NodeT> predicate, AvailabilityType availabilityType = AvailabilityType.Available)
	{
		return _nodeReferences.GetReferences(predicate, availabilityType);
	}

	public void RemoveChildrenReferencesListener(Node target)
	{
		_nodeReferences.RemoveChildrenReferencesListener(target);
	}

	public void RemoveReference(NodeT node)
	{
		_nodeReferences.RemoveReference(node);
	}

	protected override void OnStartedDispose()
	{
		_nodeReferences.Dispose();
		_nodeReferences.ReferenceAddedEvent -= OnReferenceAddedEvent;
		_nodeReferences.ReferenceRemovedEvent -= OnReferenceRemovedEvent;
		_nodeReferences = null;

		base.OnStartedDispose();
	}

	private void OnReferenceAddedEvent(NodeT node)
	{
		ReferenceAddedEvent?.Invoke(node);
	}

	private void OnReferenceRemovedEvent(NodeT node)
	{
		ReferenceRemovedEvent?.Invoke(node);
	}
}

public class NodeReferences : NodeReferences<Node>
{
	public enum AvailabilityType
	{
		Available,
		Unavailable,
		All
	}
}

public class NodeReferences<NodeT> : INodeReferences<NodeT> where NodeT : Node
{
	public event ReferenceHandler<NodeT> ReferenceAddedEvent;
	public event ReferenceHandler<NodeT> ReferenceRemovedEvent;

	private List<NodeT> _allReferences = new List<NodeT>();
	private List<NodeT> _availableReferences = new List<NodeT>();
	private List<NodeT> _unavailableReferences = new List<NodeT>();
	private List<Node> _targets = new List<Node>();

	public NodeT[] GetReferences(AvailabilityType availabilityType = AvailabilityType.Available)
	{
		return GetReferencesList(availabilityType).ToArray();
	}

	public NodeT[] GetReferences(Predicate<NodeT> predicate, AvailabilityType availabilityType = AvailabilityType.Available)
	{
		return GetReferencesList(availabilityType).FindAll(predicate).ToArray();
	}

	public void ForEach(Action<NodeT> action, AvailabilityType availabilityType = AvailabilityType.Available)
	{
		List<NodeT> listToIterate = GetReferencesList(availabilityType);
		for(int i = 0, c = listToIterate.Count; i < c; i++)
		{
			action(listToIterate[i]);
		}
	}

	public void ForEach(Action<NodeT> action, Predicate<NodeT> predicate, AvailabilityType availabilityType = AvailabilityType.Available)
	{
		List<NodeT> listToIterate = GetReferencesList(availabilityType);
		for(int i = 0, c = listToIterate.Count; i < c; i++)
		{
			NodeT node = listToIterate[i];
			if(predicate(node))
			{
				action(node);
			}
		}
	}

	public void AddChildrenReferencesListener(Node target)
	{
		if(target == null || target.IsInConditions(Condition.Disposed))
		{
			return;
		}

		if(!_targets.Contains(target))
		{
			target.ChildAddedEvent += OnTargetAddedChildEvent;
			target.ChildRemovedEvent += OnTargetRemovedChildEvent;
			target.NodeDisposedEvent += OnTargetNodeDisposedEvent;

			foreach(NodeT child in target.GetChildren<NodeT>())
			{
				AddReference(child);
			}
		}
	}

	public void RemoveChildrenReferencesListener(Node target)
	{
		if(target == null)
		{
			return;
		}

		if(_targets.Remove(target))
		{
			target.NodeDisposedEvent -= OnTargetNodeDisposedEvent;
			target.ChildRemovedEvent -= OnTargetRemovedChildEvent;
			target.ChildAddedEvent -= OnTargetAddedChildEvent;
			
			foreach(NodeT child in target.GetChildren<NodeT>())
			{
				RemoveReference(child);
			}
		}
	}

	public void AddReference(NodeT node)
	{
		if(CanAddReference(node, out string validationError))
		{
			_allReferences.Add(node);
			AssignToNodeList(node);
			node.NodeConditionChangedEvent += OnReferenceNodeConditionChangedEvent;
			ReferenceAddedEvent?.Invoke(node);
		}
		else
		{
			Debug.LogError(validationError);
		}
	}

	public void RemoveReference(NodeT node)
	{
		if(CanRemoveReference(node, out string validationError) && _allReferences.Remove(node))
		{
			AssignToNodeList(node);
			node.NodeConditionChangedEvent -= OnReferenceNodeConditionChangedEvent;
			ReferenceRemovedEvent?.Invoke(node);
		}
	}

	public bool CanAddReference(NodeT node, out string validationError)
	{
		if(_allReferences.Contains(node))
		{
			validationError = $"{this} already contains a reference to {node}";
			return false;
		}

		validationError = string.Empty;
		return true;
	}

	public bool CanRemoveReference(NodeT node, out string validationError)
	{
		if(!_allReferences.Contains(node))
		{
			validationError = $"{this} has no reference to {node}";
			return false;
		}

		validationError = string.Empty;
		return true;
	}

	public void Dispose()
	{
		for(int i = _targets.Count - 1; i >= 0; i--)
		{
			RemoveChildrenReferencesListener(_targets[i]);
		}

		for(int i = _allReferences.Count - 1; i >= 0; i--)
		{
			RemoveReference(_allReferences[i]);
		}

		ReferenceAddedEvent = null;
		ReferenceRemovedEvent = null;
	}

	private List<NodeT> GetReferencesList(AvailabilityType availabilityType)
	{
		switch(availabilityType)
		{
			case AvailabilityType.Available:
				return _availableReferences;
			case AvailabilityType.Unavailable:
				return _unavailableReferences;
			case AvailabilityType.All:
				return _allReferences;
		}
		return new List<NodeT>();
	}

	private void OnReferenceNodeConditionChangedEvent(Node node, Condition newCondition, Condition previousCondition)
	{
		if(node is NodeT castedNode)
		{
			if(newCondition == Condition.Destroying)
			{
				RemoveReference(castedNode);
			}
			else
			{
				AssignToNodeList(castedNode);
			}
		}
	}

	private void OnTargetAddedChildEvent(Node parent, Node child, int index)
	{
		if(child is NodeT castedChild)
		{
			AddReference(castedChild);
		}
	}

	private void OnTargetRemovedChildEvent(Node parent, Node child, int index)
	{
		if(child is NodeT castedChild)
		{
			RemoveReference(castedChild);
		}
	}

	private void OnTargetNodeDisposedEvent(Node node)
	{
		RemoveChildrenReferencesListener(node);
	}

	private void AssignToNodeList(NodeT node)
	{
		if(!_allReferences.Contains(node))
		{
			_availableReferences.Remove(node);
			_unavailableReferences.Remove(node);
			return;
		}

		switch(node.NodeCondition)
		{
			case Condition.Available:
				if(!_availableReferences.Contains(node))
				{
					_unavailableReferences.Remove(node);
					_availableReferences.Add(node);
				}
				break;
			case Condition.Unavailable:
				if(!_unavailableReferences.Contains(node))
				{
					_availableReferences.Remove(node);
					_unavailableReferences.Add(node);
				}
				break;
		}
	}
}

namespace NodeSystem.NodeReferencesInternal
{
	public delegate void ReferenceHandler<NodeT>(NodeT node) where NodeT : Node;

	public interface INodeReferences<NodeT> : IDisposable where NodeT : Node
	{
		event ReferenceHandler<NodeT> ReferenceAddedEvent;
		event ReferenceHandler<NodeT> ReferenceRemovedEvent;

		NodeT[] GetReferences(AvailabilityType availabilityType = AvailabilityType.Available);

		NodeT[] GetReferences(Predicate<NodeT> predicate, AvailabilityType availabilityType = AvailabilityType.Available);

		void ForEach(Action<NodeT> action, AvailabilityType availabilityType = AvailabilityType.Available);

		void ForEach(Action<NodeT> action, Predicate<NodeT> predicate, AvailabilityType availabilityType = AvailabilityType.Available);

		void AddChildrenReferencesListener(Node target);

		void RemoveChildrenReferencesListener(Node target);

		void AddReference(NodeT node);

		void RemoveReference(NodeT node);

		bool CanAddReference(NodeT node, out string validationError);

		bool CanRemoveReference(NodeT node, out string validationError);
	}
}