using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class FSMSlimeKing : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 15f;
		[SerializeField] private float attackPatternCooldown = 4f; // 다음 공격 패턴까지의 대기 시간
		[SerializeField] private bool isSpriteLookLeft = false;

		// 공격 패턴 정의
		private enum AttackPattern { Projectile, Dash, Pull }
		private AttackPattern _currentPattern;

		private float _lastPatternExecutionTime;

		public FSMSlimeKing(UnitObject unitObject) : base(unitObject) { }
		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void Init()
		{
			// BT 노드 인스턴스 생성
			BT_Idle _idle = new(UnitObject);
			BT_MoveToPlayer _moveToPlayer = new(UnitObject);
			BT_ProjectileAttack _projectileAttack = new(UnitObject);
			BT_DashAttack _dashAttack = new(UnitObject);
			BT_PullAttack _pullAttack = new(UnitObject);

			// --- 상태별 이벤트 설정 ---
			// Idle 상태: 플레이어 감지
			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				_idle.Update();
				CanSeePlayer();
			});

			// Attack 상태 진입 시: 첫 공격 패턴 선택
			SetStateEvent(FSMStateCommon.Attack, StateEvent.Enter, ChooseNextAttackPattern);

			// Attack 상태 업데이트: 현재 패턴 실행 및 다음 패턴 준비
			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				// 플레이어가 범위를 벗어나면 Idle로 전환
				CanSeePlayer();
				if (IsCurState(FSMStateCommon.Idle)) return;

				// 현재 선택된 패턴 실행
				ExecuteCurrentPattern();

				// 쿨다운이 끝나면 다음 패턴 선택
				if (Time.time > _lastPatternExecutionTime + attackPatternCooldown)
				{
					ChooseNextAttackPattern();
				}
			});

			// BT 노드 초기화
			_idle.Init(isSpriteLookLeft: isSpriteLookLeft);
			_moveToPlayer.Init(isSpriteLookLeft: isSpriteLookLeft);
			// 각 공격 BT 노드 초기화 (필요 시)
			// _projectileAttack.Init(...);
			// _dashAttack.Init(...);
			// _pullAttack.Init(...);

			void ExecuteCurrentPattern()
			{
				switch (_currentPattern)
				{
					case AttackPattern.Projectile:
						_projectileAttack.Update();
						break;
					case AttackPattern.Dash:
						_dashAttack.Update();
						break;
					case AttackPattern.Pull:
						_pullAttack.Update();
						break;
				}
			}
		}

		/// <summary>
		/// 다음 공격 패턴을 랜덤하게 선택하고 타이머를 리셋합니다.
		/// </summary>
		private void ChooseNextAttackPattern()
		{
			var patterns = Enum.GetValues(typeof(AttackPattern));
			_currentPattern = (AttackPattern)patterns.GetValue(Random.Range(0, patterns.Length));
			_lastPatternExecutionTime = Time.time;
			Debug.Log($"Next Pattern: {_currentPattern}");
		}

		private void CanSeePlayer()
		{
			if (Player.Instance == null) return;
			float distanceToPlayer = Vector3.Distance(UnitObject.transform.position, Player.Instance.transform.position);

			if (distanceToPlayer < attackRange)
			{
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