using UnityEngine;

namespace WitchMendokusai
{
	public class WispFSM : StateMachine<TempState>
	{
		[SerializeField] private float attackRange = 10f;

		private BT_Idle idle;
		private BT_RangeAttack attack;

		private void Awake()
		{
			UnitObject unitObject = GetComponent<UnitObject>();

			idle = new(unitObject);
			attack = new(unitObject);

			SetStateEvent(TempState.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.Update();
			});

			SetStateEvent(TempState.Attack, StateEvent.Update, () =>
			{
				// CanSeePlayer();
				attack.Update();
			});
		}

		protected override void Init()
		{
			idle.Init();
			attack.Init(attackRange);
		}

		private void CanSeePlayer()
		{
			if (Vector3.Distance(transform.position, Player.Instance.transform.position) < attackRange)
			{
				if (currentState != TempState.Attack)
					ChangeState(TempState.Attack);
			}
			else
			{
				if (currentState != TempState.Idle)
				ChangeState(TempState.Idle);
			}
		}
	}
}