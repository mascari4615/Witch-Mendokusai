using UnityEngine;

namespace WitchMendokusai
{
	public class NPCFSM : StateMachine<TempState>
	{
		private void Awake()
		{
			UnitObject unitObject = GetComponent<UnitObject>();

			SetStateEvent(TempState.Idle, StateEvent.Update, () =>
			{
			});
		}

		protected override void Init()
		{
		}
	}
}