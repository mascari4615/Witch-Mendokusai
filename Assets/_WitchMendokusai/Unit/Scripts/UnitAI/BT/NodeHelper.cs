using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
		public static ActionNode Action(Action action) => new(action);

		public static IfActionNode IfAction(Func<bool> condition, Action action)
			=> new(condition, action);
		public static IfElseActionNode IfElseAction(Func<bool> condition, Action ifAction, Action ifElseAction)
			=> new(condition, ifAction, ifElseAction);

		public static WaitNode Wait(float duration) => new(duration);
	}
}