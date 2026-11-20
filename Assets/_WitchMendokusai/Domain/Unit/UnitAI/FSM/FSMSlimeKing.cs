using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 슬라임 왕. 사거리 밖이면 추적, 안이면 패턴 하나 -> 대기 -> 거리 다시 판단
	/// - 패턴: 직선 투척 (Projectile), 돌진, 슬라임 발사, 빨아들이기. 처음 하나만 무작위, 이후 SlimeKingAttackConfig.PatternOrder 순서
	/// - 돌진 회복과 빨아들이기 경직에 같은 반격 창 표시 (CounterWindowFx)
	/// - 돌진은 준비 때 방향 고정. 도주 계산이 안 맞으면 (너무 멂) 돌진 대신 더 다가감
	/// - 개발 창 boss 명령이 ForcePattern 으로 다음 패턴을 지정
	/// </summary>
	public class FSMSlimeKing : FSM<FSMStateCommon>
	{
		[SerializeField] private bool isSpriteLookLeft = false;
		[SerializeField, Tooltip("공격 수치 한 벌")] private SlimeKingAttackConfig attackConfig;

		private PlayerProvider playerProvider;
		private SlimeKingShotPattern shotPattern;
		private BT_Dash dashBehavior;
		private SlimeKingLaunchPattern launchPattern;
		private SlimeKingPullPattern pullPattern;
		private SlimeKingPattern currentPattern;
		private int patternIndex;
		private bool hasPreviousPattern;
		// 첫 패턴 무작위 칸. 엿보기와 고르기가 같은 값을 보게 한 번만 굴림 (-1 이면 아직)
		private int firstIndex = -1;
		private bool hasForcedPattern;
		private bool forcedLoop;
		private SlimeKingPattern forcedPattern;
		private float plannedDashDuration;
		private float plannedDashPreDelay;

		public SlimeKingAttackConfig Config => attackConfig;
		public SlimeKingPattern CurrentPattern => currentPattern;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		/// <summary> 다음 공격을 이 패턴으로. loop 면 해제 전까지 계속 이 패턴. 사거리와 무관하게 바로 공격 상태 </summary>
		public void ForcePattern(SlimeKingPattern pattern, bool loop)
		{
			forcedPattern = pattern;
			hasForcedPattern = true;
			forcedLoop = loop;
			ChangeState(FSMStateCommon.Attack);
		}

		public void ClearForcedPattern()
		{
			hasForcedPattern = false;
			forcedLoop = false;
		}

		protected override void InitFSMEvent()
		{
			BT_MoveToPlayer moveToPlayer = new(UnitObject, playerProvider, isSpriteLookLeft);
			SlimeKingSummons summons = new(attackConfig);
			shotPattern = new SlimeKingShotPattern(UnitObject, playerProvider, attackConfig, summons);
			launchPattern = new SlimeKingLaunchPattern(UnitObject, playerProvider, attackConfig, summons);
			pullPattern = new SlimeKingPullPattern(UnitObject, playerProvider, attackConfig);

			SetStateEvent(FSMStateCommon.Idle, StateEvent.Enter, CancelPatterns);
			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				if (playerProvider.Current == null)
				{
					RemoveApproachSpeed();
					UnitObject.UnitMovement.SetMoveDirection(Vector3.zero);
					return;
				}
				UpdateApproachSpeed();
				moveToPlayer.UpdateBT();
				CanSeePlayer();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Enter, EnterAttack);
			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, UpdateAttack);
			SetStateEvent(FSMStateCommon.Attack, StateEvent.Exit, () =>
			{
				// 돌진 도중 끊기면 밀림도 같이 멈춤
				if (currentPattern == SlimeKingPattern.Dash)
					UnitObject.UnitMovement.CancelImpulse();
				CancelPatterns();
			});

			float timer = 0f;
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Enter, () =>
			{
				timer = 0f;
				RemoveApproachSpeed();
				UnitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			});
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Update, () =>
			{
				timer += BTRunner.TICK;
				if (timer < attackConfig.WaitDuration)
					return;
				timer = 0f;
				if (hasForcedPattern)
				{
					ChangeState(FSMStateCommon.Attack);
					return;
				}
				if (CanSeePlayer() == false && IsCurState(FSMStateCommon.Wait))
					ChangeState(FSMStateCommon.Idle);
			});
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Exit, () => timer = 0f);
		}

		private void EnterAttack()
		{
			RemoveApproachSpeed();
			int previousIndex = patternIndex;
			bool previousExists = hasPreviousPattern;
			bool forced = ChooseNextPattern();
			Vector3 target = playerProvider.Current != null ? playerProvider.Current.transform.position : transform.position;

			if (currentPattern == SlimeKingPattern.Dash && TryPlanDash(target) == false)
			{
				if (forced)
				{
					// 지정 돌진은 멀어도 실행. 시간과 거리 상한까지만
					plannedDashDuration = Mathf.Min(plannedDashDuration, attackConfig.MaximumDashDuration, attackConfig.MaximumDashDistance / attackConfig.DashSpeed);
				}
				else
				{
					// 너무 멀어 도주 보장이 안 됨. 순서를 되돌리고 더 다가감. 첫 패턴이면 그 칸을 유지 (다시 굴리면 공격과 추적을 오감)
					if (previousExists == false)
						firstIndex = patternIndex;
					patternIndex = previousIndex;
					hasPreviousPattern = previousExists;
					ChangeState(FSMStateCommon.Idle);
					return;
				}
			}

			switch (currentPattern)
			{
				case SlimeKingPattern.Projectile:
					shotPattern.Begin();
					break;
				case SlimeKingPattern.Dash:
					dashBehavior = new BT_Dash(UnitObject, playerProvider, attackConfig.AttackRange, attackConfig.DashSpeed, plannedDashDuration,
						onDashEnd: GoWait,
						dashPreDelay: plannedDashPreDelay,
						dashPostDelay: attackConfig.DashPostDelay,
						telegraph: attackConfig.DashTelegraph,
						telegraphWidth: attackConfig.DashTelegraphWidth,
						recoveryFx: attackConfig.CounterWindowFx,
						recoveryRadius: attackConfig.CounterWindowRadius);
					break;
				case SlimeKingPattern.Launch:
					launchPattern.Begin();
					break;
				case SlimeKingPattern.Pull:
					pullPattern.Begin();
					break;
			}
		}

		private void UpdateAttack()
		{
			if (playerProvider.Current == null)
			{
				UnitObject.UnitMovement.CancelImpulse();
				UnitObject.UnitMovement.SetMoveDirection(Vector3.zero);
				ChangeState(FSMStateCommon.Idle);
				return;
			}

			switch (currentPattern)
			{
				case SlimeKingPattern.Projectile:
					if (shotPattern.Tick(BTRunner.TICK))
						GoWait();
					break;
				case SlimeKingPattern.Dash:
					dashBehavior.UpdateBT();
					break;
				case SlimeKingPattern.Launch:
					if (launchPattern.Tick(BTRunner.TICK))
						GoWait();
					break;
				case SlimeKingPattern.Pull:
					if (pullPattern.Tick(BTRunner.TICK))
						GoWait();
					break;
			}
		}

		private void GoWait() => ChangeState(FSMStateCommon.Wait);

		/// <summary> 다음 패턴 고르기. 지정 패턴이면 true </summary>
		private bool ChooseNextPattern()
		{
			if (hasForcedPattern)
			{
				currentPattern = forcedPattern;
				if (forcedLoop == false)
					hasForcedPattern = false;
				return true;
			}

			int count = SlimeKingPatternMath.OrderCount(attackConfig.PatternOrder);
			patternIndex = hasPreviousPattern ? SlimeKingPatternMath.NextIndex(patternIndex, count) : RollFirstIndex(count);
			hasPreviousPattern = true;
			firstIndex = -1;
			currentPattern = SlimeKingPatternMath.PatternAt(attackConfig.PatternOrder, patternIndex);
			return false;
		}

		private SlimeKingPattern PeekNextPattern()
		{
			if (hasForcedPattern)
				return forcedPattern;
			int count = SlimeKingPatternMath.OrderCount(attackConfig.PatternOrder);
			if (hasPreviousPattern == false)
				return SlimeKingPatternMath.PatternAt(attackConfig.PatternOrder, RollFirstIndex(count));
			return SlimeKingPatternMath.PatternAt(attackConfig.PatternOrder, SlimeKingPatternMath.NextIndex(patternIndex, count));
		}

		private int RollFirstIndex(int count)
		{
			if (firstIndex < 0 || firstIndex >= count)
				firstIndex = Random.Range(0, count);
			return firstIndex;
		}

		private void CancelPatterns()
		{
			dashBehavior?.CancelTelegraph();
			shotPattern?.Cancel();
			launchPattern?.Cancel();
			pullPattern?.Cancel();
		}

		protected override void OnFSMDisabled()
		{
			CancelPatterns();
			RemoveApproachSpeed();
			hasPreviousPattern = false;
			firstIndex = -1;
			ClearForcedPattern();
		}

		private void UpdateApproachSpeed() => UnitObject.UnitMovement.SetMinimumInputSpeed(attackConfig.ApproachMinimumSpeed);

		private void RemoveApproachSpeed()
		{
			if (UnitObject != null)
				UnitObject.UnitMovement.SetMinimumInputSpeed(0f);
		}

		/// <summary>
		/// 돌진 시간과 준비 시간 계획. 플레이어가 최대 도주 속도로 달아나도 닿고, 최소 탈출 속도로 옆으로 피할 수 있는 시간
		/// 최대 시간이나 거리를 넘으면 false (돌진 대신 다가감)
		/// </summary>
		private bool TryPlanDash(Vector3 target)
		{
			SlimeKingAttackConfig config = attackConfig;
			plannedDashDuration = config.DashDuration;
			plannedDashPreDelay = config.DashPreDelay;
			if (playerProvider.CurrentObject == null || config.DashSpeed <= config.MaximumFleeSpeed)
				return false;
			float escapeDistance = UnitObject.UnitMovement.BodyRadius + playerProvider.CurrentObject.UnitMovement.BodyRadius + config.EscapePadding;
			float escapeSeconds = escapeDistance / config.MinimumEscapeSpeed + config.MinimumEscapeSpeed / (2f * config.EscapeAcceleration) + config.EscapeReactionSeconds + BTRunner.TICK;
			plannedDashPreDelay = Mathf.Max(plannedDashPreDelay, escapeSeconds);
			Vector3 delta = target - UnitObject.UnitMovement.Position;
			delta.y = 0f;
			float duration = (delta.magnitude + config.MaximumFleeSpeed * (plannedDashPreDelay + BTRunner.TICK)) / (config.DashSpeed - config.MaximumFleeSpeed);
			plannedDashDuration = Mathf.Max(config.DashDuration, duration);
			return plannedDashDuration <= config.MaximumDashDuration && plannedDashDuration * config.DashSpeed <= config.MaximumDashDistance;
		}

		/// <summary> 사거리 안이면 공격, 밖이면 추적. 사거리 안이면 true </summary>
		private bool CanSeePlayer()
		{
			if (playerProvider.Current == null)
				return false;
			float distanceToPlayer = Vector3.Distance(UnitObject.transform.position, playerProvider.Current.transform.position);

			if (distanceToPlayer < attackConfig.AttackRange)
			{
				if (PeekNextPattern() == SlimeKingPattern.Dash && TryPlanDash(playerProvider.Current.transform.position) == false)
				{
					if (IsCurState(FSMStateCommon.Idle) == false)
						ChangeState(FSMStateCommon.Idle);
					return false;
				}
				if (IsCurState(FSMStateCommon.Attack) == false)
					ChangeState(FSMStateCommon.Attack);
				return true;
			}

			if (IsCurState(FSMStateCommon.Idle) == false)
				ChangeState(FSMStateCommon.Idle);
			return false;
		}
	}
}
