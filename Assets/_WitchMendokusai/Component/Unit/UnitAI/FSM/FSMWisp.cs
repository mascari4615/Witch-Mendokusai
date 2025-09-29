using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class FSMWisp : FSM
	{
		[SerializeField] private float attackRange = 10f;

		private BT_Idle idle;
		private BT_RangeAttack attack;

		public FSMWisp(UnitObject unitObject) : base(unitObject) {}
		protected override FSMState DefaultState => FSMState.Idle;

		protected override void Init()
		{
			idle = new(UnitObject);
			attack = new(UnitObject);

			SetStateEvent(FSMState.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.Update();
			});

			SetStateEvent(FSMState.Attack, StateEvent.Update, () =>
			{
				// CanSeePlayer();
				attack.Update();
			});

			idle.Init();
			attack.Init(attackRange);
		}

		private void CanSeePlayer()
		{
			if (Vector3.Distance(Transform.position, Player.Instance.transform.position) < attackRange)
			{
				if (IsCurState(FSMState.Attack) == false)
					ChangeState(FSMState.Attack);
			}
			else
			{
				if (IsCurState(FSMState.Idle) == false)
					ChangeState(FSMState.Idle);
			}
		}
	}
}