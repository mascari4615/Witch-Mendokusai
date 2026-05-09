using System;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_Dash : BTRunner
	{
		private readonly float attackRange;
		private readonly float dashSpeed;
		private readonly float dashDuration;
		private event Action OnDashEnd;

		private Vector3 moveDest = Vector3.zero;

		public BT_Dash(UnitObject unitObject, float attackRange, float dashSpeed, float dashDuration, Action onDashEnd = null) : base(unitObject)
		{
			this.attackRange = attackRange;
			this.dashSpeed = dashSpeed;
			this.dashDuration = dashDuration;
			OnDashEnd = onDashEnd;
		}

		protected override Node MakeNode()
		{
			return
				Sequence
				(
					Sequence // [#] 근처에 있으면 Player향해서 Move
					(
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
						Wait(dashDuration), // 대쉬 시간
						Action(StopDash),
						Wait(0.5f), // 대쉬 후 딜레이
						Action(EndDash)
					)
				);
		}

		private BTState UpdateDestinationToPlayer()
		{
			moveDest = PlayerProvider.Instance.Current.transform.position;
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
			float distance = Vector3.Distance(PlayerProvider.Instance.Current.transform.position, unitObject.transform.position);
			bool isPlayerFar = distance > attackRange;

			return isPlayerFar;
		}

		private BTState UpdateMovementDirection()
		{
			Vector3 dir = (moveDest - unitObject.transform.position).normalized;
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
			Vector3 direction = moveDest - unitObject.transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.0001f)
				return BTState.Success;
			direction.Normalize();

			unitObject.UnitMovement.ApplyImpulse(direction * dashSpeed, dashDuration);
			return BTState.Success;
		}

		private BTState StopDash()
		{
			unitObject.UnitMovement.CancelImpulse();
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
