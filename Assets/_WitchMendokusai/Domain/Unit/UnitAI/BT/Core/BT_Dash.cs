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
		// 돌진 뒤 숨 고르기 동안 발밑 표시 (반격 창). 비우면 표시 없음
		private readonly CombatFx recoveryFx;
		private readonly float recoveryRadius;
		private event Action OnDashEnd;

		private Vector3 moveDest = Vector3.zero;
		// 준비를 시작할 때 고정한 돌진 방향 (수평 단위 벡터). 방향이 없으면 zero 이고 돌진 안 함
		private Vector3 dashDirection = Vector3.zero;
		private float dashReadyTime;
		// 돌진을 건 시점의 이동 계산 횟수. 그 뒤 한 번이라도 돌아야 직전 이동 값이 돌진의 것
		private int dashStartTick;
		private float recoveryEndTime;
		// 한 tick 에 계획 이동의 이 비율도 못 나가고 무언가에 닿아 있으면 부딪혀 멈춘 것으로 봄. 0 이면 막힘 판정 없음
		private readonly float blockedProgressRatio;
		// 주인공 쪽에서 벌어진 돌진 각도 (도)
		private readonly float angleDegrees;
		// 준비 예고의 중단 함수. 준비가 도중에 끊기면 예고도 같이 치움
		private Action stopTelegraph;
		// 이 행동이 건 밀림이 도는 중
		private bool isDashing;
		// 반격 창 표시의 중단 함수. 숨 고르기가 도중에 끊기면 같이 치움
		private Action stopRecovery;

		public BT_Dash(UnitObject unitObject, PlayerProvider playerProvider, float attackRange, float dashSpeed, float dashDuration, Action onDashEnd = null, float dashPreDelay = 0.2f, float dashPostDelay = 0.5f, CombatFx telegraph = null, float telegraphWidth = 0f, CombatFx recoveryFx = null, float recoveryRadius = 0f, float blockedProgressRatio = 0f, float angleDegrees = 0f) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attackRange = attackRange;
			this.dashSpeed = dashSpeed;
			this.dashDuration = dashDuration;
			this.dashPreDelay = dashPreDelay;
			this.dashPostDelay = dashPostDelay;
			this.telegraph = telegraph;
			this.telegraphWidth = telegraphWidth;
			this.recoveryFx = recoveryFx;
			this.recoveryRadius = recoveryRadius;
			this.blockedProgressRatio = blockedProgressRatio;
			this.angleDegrees = angleDegrees;
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
						WaitUntil(IsDashOver), // 계획 시간이 다 가거나 무언가에 부딪혀 멈추면 끝
						Action(StopDash),
						WaitUntil(IsRecoveryOver), // 대쉬 후 딜레이 (반격 창 표시와 같은 게임 시간)
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
			CancelTelegraph();
			UpdateDestinationToPlayer();
			Vector3 direction = moveDest - unitObject.transform.position;
			direction.y = 0f;
			dashDirection = direction.sqrMagnitude < 0.0001f ? Vector3.zero : Quaternion.Euler(0f, angleDegrees, 0f) * direction.normalized;
			dashReadyTime = Time.time + dashPreDelay;

			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);

			if (telegraph != null && dashDirection != Vector3.zero)
			{
				stopTelegraph = telegraph.Play(new CombatFxContext
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

		/// <summary> 준비 중이던 예고와 반격 창 표시를 치움. 상태 전환과 AI 리셋 등으로 돌진이 끊길 때 부름 (끝난 뒤 불러도 무해) </summary>
		public void CancelTelegraph()
		{
			stopTelegraph?.Invoke();
			stopTelegraph = null;
			stopRecovery?.Invoke();
			stopRecovery = null;
		}

		/// <summary> 켜진 채 끊김 (단계 전환). 예고를 치우고 이 돌진이 건 밀림도 멈춤 </summary>
		public void Cancel()
		{
			CancelTelegraph();
			if (isDashing == false)
				return;
			isDashing = false;
			unitObject.UnitMovement.CancelImpulse();
		}

		private bool IsDashReady() => Time.time >= dashReadyTime;

		private BTState Dash()
		{
			// 예고는 준비가 끝나며 스스로 마무리 (번쩍임). 돌진부터는 끊을 대상이 아님
			stopTelegraph = null;
			dashStartTick = unitObject.UnitMovement.SimulationTicks;
			if (dashDirection == Vector3.zero)
				return BTState.Success;

			unitObject.UnitMovement.ApplyImpulse(dashDirection * dashSpeed, dashDuration);
			isDashing = true;
			return BTState.Success;
		}

		/// <summary> 밀림이 다 소모됐거나 (피격 정지 동안은 소모 안 됨) 부딪혀 멈췄으면 돌진 끝 </summary>
		private bool IsDashOver()
		{
			UnitMovement movement = unitObject.UnitMovement;
			if (movement.IsExternallyDriven == false)
				return true;
			return IsBlocked(movement);
		}

		/// <summary> 캐릭터나 벽에 막혀 직전 tick 에 돌진 방향으로 거의 못 나갔는가 </summary>
		private bool IsBlocked(UnitMovement movement)
		{
			if (blockedProgressRatio <= 0f || movement.IsPaused || movement.SimulationTicks <= dashStartTick)
				return false;
			if (movement.WallContactCount == 0)
				return false;
			float expected = dashSpeed * movement.LastSimulationDeltaTime;
			if (expected <= 0f)
				return false;
			float progress = Vector3.Dot(movement.LastMoveDelta, dashDirection);
			return progress < expected * blockedProgressRatio;
		}

		private bool IsRecoveryOver() => Time.time >= recoveryEndTime;

		private BTState StopDash()
		{
			isDashing = false;
			recoveryEndTime = Time.time + dashPostDelay;
			unitObject.UnitMovement.CancelImpulse();
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			if (recoveryFx != null && dashPostDelay > 0f)
			{
				stopRecovery = recoveryFx.Play(new CombatFxContext
				{
					Position = unitObject.transform.position,
					Follow = unitObject.transform,
					Radius = recoveryRadius,
					Seconds = dashPostDelay,
				});
			}
			return BTState.Success;
		}

		private BTState EndDash()
		{
			// 반격 창 표시는 숨 고르기와 같은 길이라 스스로 마무리
			stopRecovery = null;
			OnDashEnd?.Invoke();
			return BTState.Success;
		}
	}
}
