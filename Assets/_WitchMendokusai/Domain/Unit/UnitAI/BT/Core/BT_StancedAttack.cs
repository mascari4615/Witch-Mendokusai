using System;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 예고 뒤 한동안 이어지는 공격의 박자 (끌기, 웅크리기). 추적 -> 예고 -> 자세 -> 숨 고르기 -> 대기 동안 추적
	// 자세는 시작과 끝이 짝. 기절, 사망, 풀 반납으로 끊겨도 OnStanceEnd 가 한 번 불림
	public abstract class BT_StancedAttack : BTRunner, IAttackCycle
	{
		private readonly MonsterAttack attack;
		private readonly BT_MoveToPlayer chase;
		private float phaseEndTime;
		private float readyTime;
		private bool isOpen;
		private Action stopRecovery;

		public int CompletedCount { get; private set; }
		public bool IsAttacking { get; private set; }

		protected abstract float StanceSeconds { get; }

		protected BT_StancedAttack(UnitObject unitObject, PlayerProvider playerProvider, MonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected virtual void OnWindup() { }
		protected virtual void OnStanceBegin() { }
		protected virtual void OnStanceTick() { }
		protected abstract void OnStanceEnd();

		protected override Node MakeNode() =>
			Sequence
			(
				Action(ChasePlayer),
				Condition(CanStart),
				Action(BeginWindup),
				WaitUntil(IsPhaseOver),
				Action(BeginStance),
				WaitUntil(IsStanceOver),
				Action(BeginRecovery),
				WaitUntil(IsPhaseOver),
				Action(EndAttack)
			);

		private BTState ChasePlayer()
		{
			chase.UpdateBT();
			return BTState.Success;
		}

		private bool CanStart() =>
			Time.time >= readyTime && unitObject.IsActionSuspended == false
			&& SlimeKingPatternMath.IsWithin(unitObject.transform.position, playerProvider.Current.transform.position, attack.TriggerRange);

		private BTState BeginWindup()
		{
			Interrupt();
			IsAttacking = true;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			phaseEndTime = Time.time + attack.WindupSeconds;
			isOpen = true;
			OnWindup();
			return BTState.Success;
		}

		private bool IsPhaseOver() => Time.time >= phaseEndTime;

		private BTState BeginStance()
		{
			phaseEndTime = Time.time + (unitObject.IsActionSuspended ? 0f : StanceSeconds);
			if (unitObject.IsActionSuspended == false)
				OnStanceBegin();
			return BTState.Success;
		}

		// 기절, 빙결이면 바로 끝
		private bool IsStanceOver()
		{
			if (unitObject.IsActionSuspended || Time.time >= phaseEndTime)
				return true;
			OnStanceTick();
			return false;
		}

		private BTState BeginRecovery()
		{
			CloseStance();
			phaseEndTime = Time.time + attack.RecoverySeconds;
			stopRecovery = attack.PlayRecovery(unitObject);
			return BTState.Success;
		}

		private BTState EndAttack()
		{
			stopRecovery = null;
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
			IsAttacking = false;
			return BTState.Success;
		}

		private void CloseStance()
		{
			if (isOpen == false)
				return;
			isOpen = false;
			OnStanceEnd();
		}

		// 도중에 끊김 (사망, 풀 반납, 단계 전환). 끝난 뒤 불러도 무해
		public void Interrupt()
		{
			CloseStance();
			stopRecovery?.Invoke();
			stopRecovery = null;
			IsAttacking = false;
		}
	}
}
