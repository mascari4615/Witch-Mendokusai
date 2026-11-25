using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 예고 뒤 한 번 실행하는 일반 적 공격의 박자 (WindupMonsterAttack). 추적 -> 사거리 안이고 준비됐으면 멈추고 예고 -> 실행 -> 숨 고르기 -> 대기 동안 다시 추적
	// 시간은 게임 시간 (예고 부품과 같은 시계)
	public class BT_WindupAttack : BTRunner, IAttackCycle
	{
		private readonly WindupMonsterAttack attack;
		private readonly List<Vector3> targets = new();
		private readonly List<Action> stopTelegraphs = new();
		// 때가 된 자리의 차례
		private readonly List<int> dueIndices = new();
		private readonly List<float> strikeTimes = new();
		// 자리마다 남는 물체의 수명. 안 남는 공격은 null
		private readonly List<FxHold> holds = new();
		// 아직 안 띄운 자리의 예고 시각 (TelegraphInTurn). 띄웠으면 무한대
		private readonly List<float> telegraphTimes = new();
		private float recoveryEndTime;
		private float readyTime;
		private Action stopRecovery;

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
						WaitUntil(StrikeDue),
						Action(BeginRecovery),
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
			targets.Clear();
			attack.ResolveTargets(unitObject, player.transform.position, SurvivalThreatRules.MoveVelocity(player.UnitMovement), targets);
			strikeTimes.Clear();
			holds.Clear();
			telegraphTimes.Clear();
			float windupEndTime = Time.time + attack.WindupSeconds;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			attack.PlayWindupPose(unitObject, attack.WindupSeconds);
			bool isMany = targets.Count > 1;
			bool hasObject = false;
			for (int i = 0; i < targets.Count; i++)
			{
				float delay = attack.StrikeDelay(i);
				strikeTimes.Add(windupEndTime + delay);
				holds.Add(attack.Linger.Open(strikeTimes[i]));
				CombatFxContext context = StrikeContext(i);
				context.Seconds += delay;
				bool isLater = attack.TelegraphInTurn && i > 0;
				telegraphTimes.Add(isLater ? strikeTimes[i - 1] : float.PositiveInfinity);
				if (isLater == false && attack.WindupTelegraph != null && attack.WindupSeconds > 0f)
				{
					Action stop = attack.WindupTelegraph.Play(context);
					stopTelegraphs.Add(stop);
					// 예고 판 상한에 걸려 안 보이는 자리는 때리지도, 물체를 내지도 않음 (자리 하나짜리 공격은 전과 같음)
					if (GroundTelegraph.IsSkipped(stop) && isMany)
					{
						strikeTimes[i] = float.PositiveInfinity;
						continue;
					}
				}
				if (attack.WindupFx == null || (hasObject && attack.WindupFxOnce))
					continue;
				hasObject = true;
				stopTelegraphs.Add(attack.WindupFx.Play(context));
			}
			return BTState.Success;
		}

		// 때가 된 자리를 실행하고, 남은 자리가 없으면 끝. 도중 기절, 빙결이면 남은 예고를 치우고 끝 (나머지 실행은 건너뜀)
		private bool StrikeDue()
		{
			if (unitObject.IsActionSuspended)
			{
				ClearTelegraphs();
				return true;
			}
			PlayLaterTelegraphs();
			dueIndices.Clear();
			bool isWaiting = false;
			for (int i = 0; i < targets.Count; i++)
			{
				if (float.IsPositiveInfinity(strikeTimes[i]))
					continue;
				if (strikeTimes[i] > Time.time)
				{
					isWaiting = true;
					continue;
				}
				dueIndices.Add(i);
				strikeTimes[i] = float.PositiveInfinity;
			}
			if (dueIndices.Count > 0)
			{
				PlayStrike();
				Strike();
			}
			return isWaiting == false;
		}

		private void PlayLaterTelegraphs()
		{
			for (int i = 0; i < telegraphTimes.Count; i++)
			{
				if (Time.time < telegraphTimes[i] || attack.WindupTelegraph == null)
					continue;
				telegraphTimes[i] = float.PositiveInfinity;
				CombatFxContext context = StrikeContext(i);
				context.Seconds = Mathf.Max(0.05f, strikeTimes[i] - Time.time);
				Action stop = attack.WindupTelegraph.Play(context);
				stopTelegraphs.Add(stop);
				if (GroundTelegraph.IsSkipped(stop))
					strikeTimes[i] = float.PositiveInfinity;
			}
		}

		private void Strike()
		{
			bool hasHit = false;
			foreach (int index in dueIndices)
			{
				if (hasHit == false)
					hasHit = attack.Execute(unitObject, playerProvider.CurrentObject, targets[index], index);
				if (attack.Linger.IsDangerous == false)
					continue;
				// 반격으로 죽었으면 남기지 않음
				if (unitObject.IsAlive == false)
					holds[index].Cancel(Time.time);
				else
					LingerField.Instance.Add(unitObject, attack.Linger, targets[index], holds[index]);
			}
		}

		// 맞든 빗나가든 같은 그림
		private void PlayStrike()
		{
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Strike();
			if (attack.StrikeFx == null)
				return;
			foreach (int index in dueIndices)
				attack.StrikeFx.Play(StrikeContext(index));
		}

		private CombatFxContext StrikeContext(int index)
		{
			CombatFxContext context = attack.WindupContext(unitObject, targets[index], index);
			context.Hold = holds[index];
			context.Origin = unitObject.transform.position;
			return context;
		}

		private BTState BeginRecovery()
		{
			// 예고는 차오름이 끝나며 스스로 번쩍이고 사라짐. 여기서부터 끊을 대상 아님
			stopTelegraphs.Clear();
			recoveryEndTime = Time.time + attack.RecoverySeconds;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Rest();
			// 반격으로 죽으면 표시를 띄우지 않음 (Interrupt 는 이미 지나감)
			if (unitObject.IsAlive)
				stopRecovery = attack.PlayRecovery(unitObject);
			return BTState.Success;
		}

		private BTState EndAttack()
		{
			// 반격 창 표시는 숨 고르기와 같은 길이라 스스로 마무리
			stopRecovery = null;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
			IsAttacking = false;
			return BTState.Success;
		}

		// 예고 중 끊김 (사망, 풀 반납, 패턴 이동). 남은 예고를 치움, 끝난 뒤 불러도 무해
		public void Interrupt()
		{
			ClearTelegraphs();
			stopRecovery?.Invoke();
			stopRecovery = null;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
			IsAttacking = false;
		}

		private void ClearTelegraphs()
		{
			foreach (Action stop in stopTelegraphs)
				stop?.Invoke();
			stopTelegraphs.Clear();
			// 그림은 때리는 시각보다 조금 먼저 솟아 멈추지 않음. 실행되지 못한 자리의 물체는 식어 들어가게
			for (int i = 0; i < holds.Count; i++)
			{
				if (float.IsPositiveInfinity(strikeTimes[i]) == false)
					holds[i]?.Cancel(Time.time);
			}
		}
	}
}
