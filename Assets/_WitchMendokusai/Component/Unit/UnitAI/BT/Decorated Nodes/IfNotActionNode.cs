using System;

// 날짜 : 2021-01-13 PM 4:20:16

namespace WitchMendokusai
{
	// 조건 거짓일 경우 Action 수행 및 true 리턴
	// 조건 참일 경우 false 리턴
	/// <summary> 조건에 따른 수행 노드 </summary>
	public class IfNotActionNode : DecoratorNode
	{
		public Func<bool> Condition { get; private set; }
		public Action Action { get; private set; }
		public IfNotActionNode(Func<bool> condition, Action action)
		{
			Condition = () => condition() ;
			Action = action;
		}
		public IfNotActionNode(ConditionNode condition, ActionNode action)
		{
			Condition = () =>
			{
				return condition.Condition();
			};
			Action = action.Action;
		}

		public override State OnUpdate()
		{
			State result = Condition() ? State.Success : State.Failure;
			if (result == State.Success) Action();
			return result;
		}
	}
}