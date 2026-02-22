using System;

namespace WitchMendokusai
{
	public class WaitUntilNode : Node
	{
		private readonly Func<bool> condition;

		public WaitUntilNode(Func<bool> condition)
		{
			this.condition = condition;
		}

		public override BTState OnUpdate()
		{
			return CheckCondition();
		}

		private BTState CheckCondition()
		{
			return condition() ? BTState.Success : BTState.Running;
		}
	}
}