using UnityEngine;

namespace WitchMendokusai
{
	public class FSMSlime : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 10f;
		[SerializeField] private bool isSpriteLookLeft = false;
	
		private BT_Idle idle;
		private BT_MoveToPlayer moveToPlayer;

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent()
		{
			idle = new(UnitObject, isSpriteLookLeft: isSpriteLookLeft);
			moveToPlayer = new(UnitObject, isSpriteLookLeft);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.UpdateBT();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				CanSeePlayer();
				moveToPlayer.UpdateBT();
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