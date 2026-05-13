using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class FSMSlime : FSM<FSMStateCommon>
	{
#pragma warning disable CS0414 // FSM 공격 범위 미구현 예약 필드
		[SerializeField] private float attackRange = 10f;
#pragma warning restore CS0414
		[SerializeField] private bool isSpriteLookLeft = false;

		private PlayerProvider playerProvider;
		private BT_Idle idle;
		private BT_MoveToPlayer moveToPlayer;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		protected override FSMStateCommon DefaultState => FSMStateCommon.Attack;

		protected override void InitFSMEvent()
		{
			idle = new(UnitObject, isSpriteLookLeft: isSpriteLookLeft);
			moveToPlayer = new(UnitObject, playerProvider, isSpriteLookLeft);

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
			// if (Vector3.Distance(UnitObject.transform.position, PlayerProvider.Instance.Current.transform.position) < attackRange)
			// {
			// 	if (IsCurState(FSMStateCommon.Attack) == false)
			// 		ChangeState(FSMStateCommon.Attack);
			// }
			// else
			// {
			// 	if (IsCurState(FSMStateCommon.Idle) == false)
			// 		ChangeState(FSMStateCommon.Idle);
			// }
		}
	}
}