using System;
using UnityEngine;
using UnityEngine.Rendering;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_Dash : BTRunner
	{
		private readonly float attackRange;
		private event Action OnDashEnd;

		private Vector3 moveDest = Vector3.zero;

		public BT_Dash(UnitObject unitObject, float attackRange, Action onDashEnd = null) : base(unitObject)
		{
			this.attackRange = attackRange;
			OnDashEnd = onDashEnd;
		}

		protected override Node MakeNode()
		{
			return
				Sequence
				(
					Sequence // [#] 근처에 있으면 Player향해서 Move
					(
						// Action(UpdateDestinationToPlayer),
						// Action(UpdateMovementDirection),
						// Action(UpdateSpriteFlip)
						Action(ChasePlayer),
						Condition(() => IsPlayerFar() == false)
					),
					Sequence // [#] Dash
					(
						Action(() =>
						{
							return BTState.Success;
						}),
						Wait(0.2f), // 대쉬 전 딜레이
						Action(Dash),
						Wait(0.5f), // 대쉬 시간
						Action(StopDash),
						Wait(0.5f), // 대쉬 후 딜레이
						Action(EndDash)
					)
				);
		}

		private BTState UpdateDestinationToPlayer()
		{
			moveDest = Player.Instance.transform.position;
			return BTState.Success;
		}

		private BTState ChasePlayer()
		{
			UpdateDestinationToPlayer();
			UpdateMovementDirection();
			UpdateSpriteFlip();
			return BTState.Success;
		}

		protected bool IsPlayerFar()
		{
			float distance = Vector3.Distance(Player.Instance.transform.position, unitObject.transform.position);
			bool isPlayerFar = distance > attackRange;

			return isPlayerFar;
		}

		private BTState UpdateMovementDirection()
		{
			// NavMeshAgent agent = unitObject.NavMeshAgent;

			Vector3 dir = (moveDest - unitObject.transform.position).normalized;
			// agent.destination = unitObject.transform.position + dir;

			unitObject.UnitMovement.SetMoveDirection(dir);
			return BTState.Success;
		}

		private BTState UpdateSpriteFlip()
		{
			unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();
			return BTState.Success;
		}

		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > .5f;
		}

		private BTState Dash()
		{
			UpdateDestinationToPlayer();
			UpdateMovementDirection();
			unitObject.UnitStat[UnitStatType.FORCE_MOVE]++; // 초기화
			return BTState.Success;
		}

		private BTState StopDash()
		{
			unitObject.UnitStat[UnitStatType.FORCE_MOVE] = 0;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			return BTState.Success;
		}

		private BTState EndDash()
		{
			OnDashEnd?.Invoke();
			return BTState.Success;
		}
	}
}