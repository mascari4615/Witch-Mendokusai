using UnityEngine;

namespace WitchMendokusai
{
	public class WaitNode : Node
	{
		private readonly float duration;
		private float t;

		public WaitNode(float duration)
		{
			this.duration = duration;
			t = 0;
		}

		public override BTState OnUpdate()
		{
			t += BTRunner.TICK;
			bool timeout = t >= duration;

			if (timeout)
			{
				// Debug.Log($"WaitNode Timeout : {duration}");
				t = 0;
				return BTState.Success;
			}
			else
			{
				// Debug.Log($"WaitNode Running : {duration}");
				return BTState.Running;
			}
		}
	}
}