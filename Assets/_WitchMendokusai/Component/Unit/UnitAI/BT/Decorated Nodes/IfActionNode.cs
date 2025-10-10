using System;

namespace WitchMendokusai
{
	// 조건 참일 경우 Action 수행 및 true 리턴
	// 조건 거짓일 경우 false 리턴
	/// <summary> 조건에 따른 수행 노드 </summary>
	public class IfActionNode : ActionNode
	{
		public Func<bool> Condition { get; private set; }

		public IfActionNode(Func<bool> condition, Func<BTState> action)
			: base(action)
		{
			Condition = condition;
		}
		public IfActionNode(ConditionNode condition, ActionNode action)
			: base(action.Action)
		{
			Condition = condition.Condition;
		}

		public override BTState OnUpdate()
		{
			BTState result = Condition() ? BTState.Success : BTState.Failure;
			if (result == BTState.Success) Action();
			return result;
		}
	}
}