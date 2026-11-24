using System;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 예고 뒤 한 번 실행하는 일반 적 공격의 박자 (WindupMonsterAttack). 추적 -> 사거리 안이고 준비됐으면 멈추고 예고 -> 실행 -> 숨 고르기 -> 대기 동안 다시 추적
	// 시간은 게임 시간 (예고 부품과 같은 시계)
	public class BT_WindupAttack : BTRunner, IAttackCycle
	{
		private readonly WindupMonsterAttack attack;
		private Vector3 target;
		private float windupEndTime;
		private float recoveryEndTime;
		private float readyTime;
		private Action stopTelegraph;

		public int CompletedCount { get; private set; }
		public bool IsAttacking { get; private set; }

		public BT_WindupAttack(UnitObject unitObject, PlayerProvider playerProvider, WindupMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
		}

		protected override Node MakeNode()
		{
			return
				Sequence
				(
					Sequence
					(
						Action(ChasePlayer),
						Condition(CanStart)
					),
					Sequence
					(
						Action(BeginWindup),
						WaitUntil(IsWindupOver),
						Action(Execute),
						WaitUntil(() => Time.time >= recoveryEndTime),
						Action(EndAttack)
					)
				);
		}

		private BTState ChasePlayer()
		{
			Vector3 toPlayer = playerProvider.Current.transform.position - unitObject.transform.position;
			toPlayer.y = 0f;
			unitObject.UnitMovement.SetMoveDirection(toPlayer.normalized);
			FacePlayer();
			return BTState.Success;
		}

		private void FacePlayer() => unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();

		// 기절, 빙결 중에는 준비를 시작하지 않음
		private bool CanStart()
		{
			if (Time.time < readyTime || unitObject.IsActionSuspended || attack.IsReady(unitObject) == false)
				return false;
			float range = attack.TriggerRange;
			Vector3 offset = playerProvider.Current.transform.position - unitObject.transform.position;
			offset.y = 0f;
			return offset.sqrMagnitude <= range * range;
		}

		private BTState BeginWindup()
		{
			Interrupt();
			IsAttacking = true;
			PlayerObject player = playerProvider.CurrentObject;
			target = attack.ResolveTarget(player.transform.position, SurvivalThreatRules.MoveVelocity(player.UnitMovement));
			windupEndTime = Time.time + attack.WindupSeconds;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			if (attack.WindupTelegraph != null && attack.WindupSeconds > 0f)
				stopTelegraph = attack.WindupTelegraph.Play(attack.WindupContext(unitObject, target));
			return BTState.Success;
		}

		// 예고 도중 기절, 빙결이면 예고를 치우고 바로 끝 (실행은 건너뜀)
		private bool IsWindupOver()
		{
			if (unitObject.IsActionSuspended)
			{
				Interrupt();
				return true;
			}
			return Time.time >= windupEndTime;
		}

		private BTState Execute()
		{
			// 예고는 차오름이 끝나며 스스로 번쩍이고 사라짐. 여기서부터 끊을 대상 아님
			stopTelegraph = null;
			// 예고 도중 기절, 빙결이면 실행 없이 숨 고르기로
			if (unitObject.IsActionSuspended == false)
				attack.Execute(unitObject, playerProvider.CurrentObject, target);
			recoveryEndTime = Time.time + attack.RecoverySeconds;
			return BTState.Success;
		}

		private BTState EndAttack()
		{
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
			IsAttacking = false;
			return BTState.Success;
		}

		// 예고 중 끊김 (사망, 풀 반납, 패턴 이동). 남은 예고를 치움, 끝난 뒤 불러도 무해
		public void Interrupt()
		{
			stopTelegraph?.Invoke();
			stopTelegraph = null;
		}
	}
}
