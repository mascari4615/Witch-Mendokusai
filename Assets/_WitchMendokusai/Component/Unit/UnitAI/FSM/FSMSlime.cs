using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class FSMSlime : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 10f;
		[SerializeField] private bool isSpriteLookLeft = false;
	
		private BT_Idle idle;
		private BT_MoveToPlayer moveToPlayer;

		public FSMSlime(UnitObject unitObject) : base(unitObject) { }
		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void Init()
		{
			idle = new(UnitObject);
			moveToPlayer = new(UnitObject);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.Update();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				CanSeePlayer();
				moveToPlayer.Update();
			});

			idle.Init(isSpriteLookLeft: isSpriteLookLeft);
			moveToPlayer.Init(isSpriteLookLeft: isSpriteLookLeft);
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