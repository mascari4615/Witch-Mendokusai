using System;

namespace WitchMendokusai
{
	// Core에서
	// using static WitchMendokusai.NodeHelper;
	public static class NodeHelper
	{
		public static SelectorNode Selector(params Node[] nodes) => new(nodes);
		public static SequenceNode Sequence(params Node[] nodes) => new(nodes);
		public static ParallelNode Parallel(params Node[] nodes) => new(nodes);

		public static ConditionNode Condition(Func<bool> condition) => new(condition);
		public static ConditionNode If(Func<bool> condition) => new(condition);
		public static ActionNode Action(Func<BTState> action) => new(action);

		public static IfActionNode IfAction(Func<bool> condition, Func<BTState> action)
			=> new(condition, action);
		public static IfElseActionNode IfElseAction(Func<bool> condition, Func<BTState> ifAction, Func<BTState> ifElseAction)
			=> new(condition, ifAction, ifElseAction);

		public static WaitNode Wait(float duration) => new(duration);
		public static WaitUntilNode WaitUntil(Func<bool> condition) => new(condition);
	}
}