using System;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 예고 뒤 한동안 이어지는 공격의 박자 (끌기, 웅크리기). 추적 -> 예고 -> 자세 -> 숨 고르기 -> 대기 동안 추적
	// 자세는 시작과 끝이 짝. 기절, 사망, 풀 반납, 단계 전환으로 끊겨도 OnStanceEnd 가 한 번 불림 (끊겼으면 interrupted)
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
		public bool WasCut { get; private set; }

		protected abstract float StanceSeconds { get; }

		// 자세를 시작할 때 때리는 그림으로. 끝에 때리는 종류는 false
		protected virtual bool StrikesAtBegin => true;

		protected BT_StancedAttack(UnitObject unitObject, PlayerProvider playerProvider, MonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected virtual void OnWindup() { }
		protected virtual void OnStanceBegin() { }
		protected virtual void OnStanceTick() { }
		protected abstract void OnStanceEnd(bool interrupted);

		protected override Node MakeNode() =>
			Sequence
			(
				Action(ChasePlayer),
				Condition(CanStart),
				Action(BeginWindup),
				WaitUntil(IsWindupOver),
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
			WasCut = false;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			phaseEndTime = Time.time + attack.WindupSeconds;
			isOpen = true;
			attack.PlayWindupPose(unitObject, attack.WindupSeconds);
			OnWindup();
			return BTState.Success;
		}

		private bool IsPhaseOver() => Time.time >= phaseEndTime;

		// 예고 도중 기절, 빙결이면 예고를 치우고 자세 없이 숨 고르기로
		private bool IsWindupOver()
		{
			if (unitObject.IsActionSuspended == false)
				return IsPhaseOver();
			CloseStance(true);
			return true;
		}

		private BTState BeginStance()
		{
			phaseEndTime = Time.time + (isOpen ? StanceSeconds : 0f);
			if (isOpen)
			{
				if (StrikesAtBegin && unitObject.TryGetComponent(out MonsterPoseDriver pose))
					pose.Strike();
				OnStanceBegin();
			}
			return BTState.Success;
		}

		// 기절, 빙결이면 바로 끝
		private bool IsStanceOver()
		{
			if (unitObject.IsActionSuspended)
				CloseStance(true);
			if (isOpen == false || Time.time >= phaseEndTime)
				return true;
			OnStanceTick();
			return false;
		}

		private BTState BeginRecovery()
		{
			CloseStance(false);
			phaseEndTime = Time.time + attack.RecoverySeconds;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Rest();
			stopRecovery = attack.PlayRecovery(unitObject);
			return BTState.Success;
		}

		private BTState EndAttack()
		{
			stopRecovery = null;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
			IsAttacking = false;
			return BTState.Success;
		}

		private void CloseStance(bool interrupted)
		{
			if (isOpen == false)
				return;
			isOpen = false;
			WasCut = interrupted;
			OnStanceEnd(interrupted);
		}

		// 도중에 끊김 (사망, 풀 반납, 단계 전환). 끝난 뒤 불러도 무해
		public void Interrupt()
		{
			CloseStance(true);
			stopRecovery?.Invoke();
			stopRecovery = null;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
			IsAttacking = false;
		}
	}
}
