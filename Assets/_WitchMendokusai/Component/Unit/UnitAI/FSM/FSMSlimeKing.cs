using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class FSMSlimeKing : FSM<FSMStateCommon>
	{
		[SerializeField] private float attackRange = 15f;
		[SerializeField] private bool isSpriteLookLeft = false;

		// 공격 패턴 정의
		private enum AttackPattern
		{
			Projectile, Dash, // Pull
		}
		private AttackPattern _currentPattern;

		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent()
		{
			// BT 노드 인스턴스 생성
			BT_Idle _idle = new(UnitObject, isSpriteLookLeft: isSpriteLookLeft);
			BT_MoveToPlayer _moveToPlayer = new(UnitObject, isSpriteLookLeft);
			BT_Skill _projectileAttack = new(UnitObject, 0, attackRange, () => ChangeState(FSMStateCommon.Wait));
			BT_Dash _dash = new(UnitObject, attackRange, () => ChangeState(FSMStateCommon.Wait));
			// BT_PullAttack _pullAttack = new(UnitObject, attackRange);

			// --- 상태별 이벤트 설정 ---
			SetStateEvent(FSMStateCommon.Idle, StateEvent.Update, () =>
			{
				_idle.UpdateBT();
				CanSeePlayer();
			});

			SetStateEvent(FSMStateCommon.Attack, StateEvent.Enter, ChooseNextAttackPattern);
			SetStateEvent(FSMStateCommon.Attack, StateEvent.Update, () =>
			{
				BTRunner currentBT = GetCurrentBehaviorTree();

				// 현재 선택된 패턴 실행
				currentBT.UpdateBT();

				if (currentBT.CanChangeState == false)
				{
					return;
				}

				// 플레이어가 범위를 벗어나면 Idle로 전환
				// CanSeePlayer();
				// if (IsCurState(FSMStateCommon.Idle))
				// 	return;
			});
			BTRunner GetCurrentBehaviorTree()
			{
				return _currentPattern switch
				{
					AttackPattern.Projectile => _projectileAttack,
					AttackPattern.Dash => _dash,
					// AttackPattern.Pull => _pullAttack,
					_ => throw new ArgumentOutOfRangeException()
				};
			}

			float timer = 0f;
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Enter, () => timer = 0f);
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Update, () =>
			{
				timer += BTRunner.TICK;
				if (timer >= 2f)
				{
					timer = 0f;
					Debug.Log("Wait Over, Attack!");
					ChangeState(FSMStateCommon.Attack);
				}
			});
			SetStateEvent(FSMStateCommon.Wait, StateEvent.Exit, () => timer = 0f);
		}

		/// <summary> 다음 공격 패턴을 랜덤하게 선택하고 타이머를 리셋합니다. </summary>
		private void ChooseNextAttackPattern()
		{
			var patterns = Enum.GetValues(typeof(AttackPattern));
			_currentPattern = (AttackPattern)patterns.GetValue(Random.Range(0, patterns.Length));
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