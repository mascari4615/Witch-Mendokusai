using System;

namespace WitchMendokusai
{
	/// <summary> 조건 검사 노드 </summary>
	public class ConditionNode : Node
	{
		public Func<bool> Condition { get; protected set; }
		public ConditionNode(Func<bool> condition)
		{
			Condition = condition;
		}

		// Func <=> ConditionNode 타입 캐스팅
		public static implicit operator ConditionNode(Func<bool> condition) => new(condition);
		public static implicit operator Func<bool>(ConditionNode condition) => new(condition.Condition);

		// Decorated Node Creator Methods
		public IfActionNode Action(Func<BTState> action)
			=> new(Condition, action);

		public IfSequenceNode Sequence(params Node[] nodes)
			=> new(Condition, nodes);

		public IfSelectorNode Selector(params Node[] nodes)
			=> new(Condition, nodes);

		public IfParallelNode Parallel(params Node[] nodes)
			=> new(Condition, nodes);

		public override BTState OnUpdate()
		{
			return Condition() ? BTState.Success : BTState.Failure;
		}
	}
}