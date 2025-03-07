using UnityEngine;

namespace WitchMendokusai
{
	public class SlimeFSM : StateMachine<TempState>
	{
		[SerializeField] private float attackRange = 10f;
		[SerializeField] private bool isSpriteLookLeft = false;
	
		private BT_Idle idle;
		private BT_MoveToPlayer moveToPlayer;

		private void Awake()
		{
			UnitObject unitObject = GetComponent<UnitObject>();

			idle = new(unitObject);
			moveToPlayer = new(unitObject);

			SetStateEvent(TempState.Idle, StateEvent.Update, () =>
			{
				CanSeePlayer();
				idle.Update();
			});

			SetStateEvent(TempState.Attack, StateEvent.Update, () =>
			{
				CanSeePlayer();
				moveToPlayer.Update();
			});
		}

		protected override void Init()
		{
			idle.Init(isSpriteLookLeft: isSpriteLookLeft);
			moveToPlayer.Init(isSpriteLookLeft: isSpriteLookLeft);
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