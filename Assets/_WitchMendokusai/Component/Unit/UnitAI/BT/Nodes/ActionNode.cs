using System;

namespace WitchMendokusai
{
	/// <summary> 행동 수행 노드 </summary>
	public class ActionNode : Node
	{
		public Action Action { get; protected set; }
		public ActionNode(Action action)
		{
			Action = action;
		}

		public override State OnUpdate()
		{
			Action();
			return State.Success;
		}

		// Action <=> ActionNode 타입 캐스팅
		public static implicit operator ActionNode(Action action) => new(action);
		public static implicit operator Action(ActionNode action) => new(action.Action);
	}
}