using System;

namespace WitchMendokusai
{
	// 조건 참일 경우 IfAction 수행 및 true 리턴
	// 조건 거짓일 경우 ElseAction 수행 및 false 리턴
	/// <summary> 조건에 따른 수행 노드 </summary>
	public class IfElseActionNode : Node
	{
		public Func<bool> Condition { get; private set; }
		public Func<BTState> IfAction { get; private set; }
		public Func<BTState> ElseAction { get; private set; }

		public IfElseActionNode(Func<bool> condition, Func<BTState> ifAction, Func<BTState> elseAction)
		{
			Condition = condition;
			IfAction = ifAction;
			ElseAction = elseAction;
		}

		public IfElseActionNode(ConditionNode condition, ActionNode ifAction, ActionNode elseAction)
		{
			Condition = condition.Condition;
			IfAction = ifAction.Action;
			ElseAction = elseAction.Action;
		}

		public override BTState OnUpdate()
		{
			BTState result = Condition()? BTState.Success : BTState.Failure;

			if (result == BTState.Success) IfAction();
			else ElseAction();

			return result;
		}
	}
}