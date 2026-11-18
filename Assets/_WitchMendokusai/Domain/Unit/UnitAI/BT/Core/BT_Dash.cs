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
		// 돌진 전 바닥 선형 예고. 비우면 예고 없음. 길이는 dashSpeed x dashDuration
		private readonly CombatFx telegraph;
		private readonly float telegraphWidth;
		private event Action OnDashEnd;

		private Vector3 moveDest = Vector3.zero;
		// 준비를 시작할 때 고정한 돌진 방향 (수평 단위 벡터). 방향이 없으면 zero 이고 돌진 안 함
		private Vector3 dashDirection = Vector3.zero;
		private float dashReadyTime;

		public BT_Dash(UnitObject unitObject, PlayerProvider playerProvider, float attackRange, float dashSpeed, float dashDuration, Action onDashEnd = null, float dashPreDelay = 0.2f, float dashPostDelay = 0.5f, CombatFx telegraph = null, float telegraphWidth = 0f) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attackRange = attackRange;
			this.dashSpeed = dashSpeed;
			this.dashDuration = dashDuration;
			this.dashPreDelay = dashPreDelay;
			this.dashPostDelay = dashPostDelay;
			this.telegraph = telegraph;
			this.telegraphWidth = telegraphWidth;
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
						Action(PrepareDash), // 방향 고정, 예고 시작
						WaitUntil(IsDashReady), // 대쉬 전 딜레이 (예고와 맞게 게임 시간으로 잼)
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


		/// <summary> 돌진 방향을 지금 플레이어 쪽으로 고정하고 멈춰서 예고를 띄움. 돌진은 이 방향 그대로 </summary>
		private BTState PrepareDash()
		{
			UpdateDestinationToPlayer();
			Vector3 direction = moveDest - unitObject.transform.position;
			direction.y = 0f;
			dashDirection = direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
			dashReadyTime = Time.time + dashPreDelay;

			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);

			if (telegraph != null && dashDirection != Vector3.zero)
			{
				telegraph.Play(new CombatFxContext
				{
					Position = unitObject.transform.position,
					Direction = dashDirection,
					Follow = unitObject.transform,
					Length = dashSpeed * dashDuration,
					Width = telegraphWidth,
					Seconds = dashPreDelay,
				});
			}
			return BTState.Success;
		}

		private bool IsDashReady() => Time.time >= dashReadyTime;

		private BTState Dash()
		{
			if (dashDirection == Vector3.zero)
				return BTState.Success;

			unitObject.UnitMovement.ApplyImpulse(dashDirection * dashSpeed, dashDuration);
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
