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
		// 돌진 전 「멈칫」과 돌진 후 「숨 고르기」. 피할 틈을 주는 값이라 속도·시간과 같이 만져야 한다.
		private readonly float dashPreDelay;
		private readonly float dashPostDelay;
		private event Action OnDashEnd;

		private Vector3 moveDest = Vector3.zero;

		public BT_Dash(UnitObject unitObject, PlayerProvider playerProvider, float attackRange, float dashSpeed, float dashDuration, Action onDashEnd = null, float dashPreDelay = 0.2f, float dashPostDelay = 0.5f) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attackRange = attackRange;
			this.dashSpeed = dashSpeed;
			this.dashDuration = dashDuration;
			this.dashPreDelay = dashPreDelay;
			this.dashPostDelay = dashPostDelay;
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
						Wait(dashPreDelay), // 대쉬 전 딜레이
						Action(Dash),
						Wait(dashDuration), // 대쉬 시간
						Action(StopDash),
						Wait(dashPostDelay), // 대쉬 후 딜레이
						Action(EndDash)
					)
				);
		}

		private BTState UpdateDestinationToPlayer()
		{
			moveDest = playerProvider.Current.transform.position;
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
			float distance = Vector3.Distance(playerProvider.Current.transform.position, unitObject.transform.position);
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
