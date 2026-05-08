using UnityEngine;

namespace WitchMendokusai
{
	public class FSMWisp : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 10f;

		private BT_Idle idle;
		private BT_Skill attack;

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent()
		{
			idle = new(UnitObject);
			attack = new(UnitObject, 0, attackRange);

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
			if (Vector3.Distance(UnitObject.transform.position, Player.Instance.transform.position) < attackRange)
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