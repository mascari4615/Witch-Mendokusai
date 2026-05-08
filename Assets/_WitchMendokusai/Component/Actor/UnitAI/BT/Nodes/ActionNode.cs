using System;

namespace WitchMendokusai
{
	/// <summary> 행동 수행 노드 </summary>
	public class ActionNode : Node
	{
		public Func<BTState> Action { get; protected set; }
		public ActionNode(Func<BTState> action)
		{
			Action = action;
		}

		public override BTState OnUpdate()
		{
			return Action();
		}

		// Action <=> ActionNode 타입 캐스팅
		public static implicit operator ActionNode(Func<BTState> action) => new(action);
		public static implicit operator Func<BTState>(ActionNode action) => new(action.Action);
	}
}