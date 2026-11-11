using System;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class FSMSlimeKing : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 15f;
		[SerializeField] private bool isSpriteLookLeft = false;

		[Header("Dash Attack")]
		[SerializeField] private float dashSpeed = 15f;
		[SerializeField] private float dashDuration = 0.5f;

		[Header("Wait")]
		[SerializeField] private float waitDuration = 2f;
		[SerializeField] private SlimeKingAttackConfig attackConfig;
		private LineRenderer dashTelegraph;
		private bool hasPreviousPattern;
		private float plannedDashDuration;
		private float plannedDashPreDelay;

		// 공격 패턴 정의
		private enum AttackPattern
		{
			Projectile, Dash,
		}
		private AttackPattern _currentPattern;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent()
		{
			// BT 노드 인스턴스 생성
			BT_MoveToPlayer _moveToPlayer = new(UnitObject, playerProvider, isSpriteLookLeft);
			BT_Skill _projectileAttack = new(UnitObject, playerProvider, 0, attackRange, () => ChangeState(FSMStateCommon.Wait));
			BT_Dash _dash = null;
			SetStateEvent(FSMStateCommon.Idle, StateEvent.Enter, () =>
			{
				HideDashTelegraph();
			});

			// --- 상태별 이벤트 설정 ---
			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				if (playerProvider.Current == null)
				{
					RemoveApproachSpeed();
					UnitObject.UnitMovement.SetMoveDirection(Vector3.zero);
					return;
				}
				UpdateApproachSpeed();
				_moveToPlayer.UpdateBT();
				CanSeePlayer();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Enter, () =>
			{
				RemoveApproachSpeed();
				AttackPattern previousPattern = _currentPattern;
				bool previousPatternExists = hasPreviousPattern;
				ChooseNextAttackPattern();
				Vector3 target = playerProvider.Current != null ? playerProvider.Current.transform.position : transform.position;
				if (_currentPattern == AttackPattern.Dash && TryPlanDash(target) == false)
				{
					_currentPattern = previousPattern;
					hasPreviousPattern = previousPatternExists;
					ChangeState(FSMStateCommon.Idle);
					return;
				}
				_projectileAttack = new BT_Skill(UnitObject, playerProvider, 0, attackRange, () => ChangeState(FSMStateCommon.Wait));
				_dash = new BT_Dash(UnitObject, playerProvider, attackRange, dashSpeed, plannedDashDuration,
					onDashEnd: () => ChangeState(FSMStateCommon.Wait),
					dashPreDelay: plannedDashPreDelay,
					dashPostDelay: attackConfig != null ? attackConfig.DashPostDelay : 0.5f,
					lockAimBeforeDelay: true, onPrepare: ShowDashTelegraph,
					onDashStart: HideDashTelegraph, onRecovery: ShowRecovery, preparedTarget: target);
			});
			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				if (playerProvider.Current == null)
				{
					UnitObject.UnitMovement.CancelImpulse();
					UnitObject.UnitMovement.SetMoveDirection(Vector3.zero);
					ChangeState(FSMStateCommon.Idle);
					return;
				}
				if (_currentPattern == AttackPattern.Projectile)
				{
					UpdateApproachSpeed();
				}
				BTRunner currentBT = GetCurrentBehaviorTree();

				// 현재 선택된 패턴 실행
				currentBT.UpdateBT();

				if (currentBT.CanChangeState == false)
				{
					return;
				}
			});
			BTRunner GetCurrentBehaviorTree()
			{
				return _currentPattern switch
				{
					AttackPattern.Projectile => _projectileAttack,
					AttackPattern.Dash => _dash,
					_ => throw new ArgumentOutOfRangeException()
				};
			}

			float timer = 0f;
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Enter, () =>
			{
				timer = 0f;
				RemoveApproachSpeed();
				HideDashTelegraph();
				UnitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			});
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Update, () =>
			{
				timer += BTRunner.TICK;
				if (timer >= waitDuration)
				{
					timer = 0f;
					CanSeePlayer();
				}
			});
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Exit, () => timer = 0f);
		}

		/// <summary> 다음 공격 패턴을 랜덤하게 선택하고 타이머를 리셋합니다. </summary>
		private void ChooseNextAttackPattern()
		{
			_currentPattern = hasPreviousPattern
				? (_currentPattern == AttackPattern.Dash ? AttackPattern.Projectile : AttackPattern.Dash)
				: (AttackPattern)Random.Range(0, 2);
			hasPreviousPattern = true;
		}

		private void ShowDashTelegraph(Vector3 direction)
		{
			if (attackConfig == null || attackConfig.TelegraphMaterial == null) return;
			if (dashTelegraph == null)
			{
				GameObject telegraphObject = new GameObject("Dash Telegraph");
				telegraphObject.transform.SetParent(transform, false);
				telegraphObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				dashTelegraph = telegraphObject.AddComponent<LineRenderer>();
				dashTelegraph.alignment = LineAlignment.TransformZ;
				dashTelegraph.useWorldSpace = true;
				dashTelegraph.positionCount = 2;
				dashTelegraph.sharedMaterial = attackConfig.TelegraphMaterial;
				dashTelegraph.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				dashTelegraph.receiveShadows = false;
			}
			CapsuleCollider capsule = GetComponent<CapsuleCollider>();
			Vector3 origin = transform.position;
			float width = capsule != null ? Mathf.Min(capsule.bounds.size.x, capsule.bounds.size.z) : 0f;
			if (capsule != null) origin.y = capsule.bounds.min.y + capsule.contactOffset;
			dashTelegraph.startWidth = width;
			dashTelegraph.endWidth = width;
			dashTelegraph.startColor = attackConfig.PrepareColor;
			dashTelegraph.endColor = attackConfig.PrepareColor;
			dashTelegraph.SetPosition(0, origin);
			dashTelegraph.SetPosition(1, origin + direction * dashSpeed * plannedDashDuration);
			dashTelegraph.enabled = true;
		}

		private void ShowRecovery()
		{
			if (dashTelegraph == null || attackConfig == null) return;
			dashTelegraph.startColor = attackConfig.RecoveryColor;
			dashTelegraph.endColor = attackConfig.RecoveryColor;
			dashTelegraph.enabled = true;
		}

		private void HideDashTelegraph()
		{
			if (dashTelegraph != null) dashTelegraph.enabled = false;
		}

		protected override void OnFSMDisabled()
		{
			RemoveApproachSpeed();
			hasPreviousPattern = false;
			HideDashTelegraph();
		}

		private void UpdateApproachSpeed()
		{
			UnitObject.UnitMovement.SetMinimumInputSpeed(attackConfig != null ? attackConfig.ApproachMinimumSpeed : 0f);
		}

		private void RemoveApproachSpeed()
		{
			if (UnitObject != null) UnitObject.UnitMovement.SetMinimumInputSpeed(0f);
		}

		private bool TryPlanDash(Vector3 target)
		{
			plannedDashDuration = dashDuration;
			plannedDashPreDelay = attackConfig != null ? attackConfig.DashPreDelay : 0.2f;
			if (attackConfig == null) return true;
			if (playerProvider.CurrentObject == null || attackConfig.MinimumEscapeSpeed <= 0f || attackConfig.EscapeAcceleration <= 0f || attackConfig.MaximumFleeSpeed < 0f || dashSpeed <= attackConfig.MaximumFleeSpeed) return false;
			float escapeDistance = UnitObject.UnitMovement.BodyRadius + playerProvider.CurrentObject.UnitMovement.BodyRadius + attackConfig.EscapePadding;
			plannedDashPreDelay = Mathf.Max(plannedDashPreDelay, escapeDistance / attackConfig.MinimumEscapeSpeed + attackConfig.MinimumEscapeSpeed / (2f * attackConfig.EscapeAcceleration) + attackConfig.EscapeReactionSeconds + BTRunner.TICK);
			Vector3 delta = target - UnitObject.UnitMovement.Position;
			delta.y = 0f;
			float duration = (delta.magnitude + attackConfig.MaximumFleeSpeed * (plannedDashPreDelay + BTRunner.TICK)) / (dashSpeed - attackConfig.MaximumFleeSpeed);
			plannedDashDuration = Mathf.Max(dashDuration, duration);
			return plannedDashDuration <= attackConfig.MaximumDashDuration && plannedDashDuration * dashSpeed <= attackConfig.MaximumDashDistance;
		}

		private void CanSeePlayer()
		{
			if (playerProvider.Current == null) return;
			float distanceToPlayer = Vector3.Distance(UnitObject.transform.position, playerProvider.Current.transform.position);

			if (distanceToPlayer < attackRange)
			{
				if (hasPreviousPattern && _currentPattern == AttackPattern.Projectile && TryPlanDash(playerProvider.Current.transform.position) == false)
				{
					if (IsCurState(FSMStateCommon.Idle) == false) ChangeState(FSMStateCommon.Idle);
					return;
				}
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
