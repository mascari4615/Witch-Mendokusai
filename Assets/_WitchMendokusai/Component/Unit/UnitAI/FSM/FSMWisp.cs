using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class FSMWisp : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 10f;

		private BT_Idle idle;
		private BT_ProjectileAttack attack;

		public FSMWisp(UnitObject unitObject) : base(unitObject) {}
		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void Init()
		{
			idle = new(UnitObject);
			attack = new(UnitObject);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.Update();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				// CanSeePlayer();
				attack.Update();
			});

			idle.Init();
			attack.Init(attackRange);
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