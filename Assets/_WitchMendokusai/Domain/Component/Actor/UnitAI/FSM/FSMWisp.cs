using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class FSMWisp : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 10f;

		private PlayerProvider playerProvider;
		private BT_Idle idle;
		private BT_Skill attack;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent()
		{
			idle = new(UnitObject);
			attack = new(UnitObject, playerProvider, 0, attackRange);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.UpdateBT();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				// CanSeePlayer();
				attack.UpdateBT();
			});
		}

		private void CanSeePlayer()
		{
			if (Vector3.Distance(UnitObject.transform.position, playerProvider.Current.transform.position) < attackRange)
			{
				if (IsCurState(FSMStateCommon.Attack) == false)
					ChangeState(FSMStateCommon.Attack);
			}
			else
			{
				if (IsCurState(FSMStateCommon.Idle) == false)
					ChangeState(FSMStateCommon.Idle);
			}
		}
	}
}
