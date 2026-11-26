using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 부술 물체 (WardMonsterAttack). 대기 동안 추적 -> 사거리 안이면 멈추고 물체를 세움, 예고 -> 큰 공격 (그 예고 동안도 부수면 취소) -> 대기
	// 다 부서지면 큰 공격을 끊고 숨 고르기 동안 주저앉음 (반격 창)
	public class BT_WardAttack : BTRunner, IAttackCycle
	{
		private enum Stage
		{
			Waiting = 0,
			Window = 1,
			Payoff = 2,
			Staggered = 3,
		}

		private readonly WardMonsterAttack attack;
		private readonly BT_MoveToPlayer chase;
		private readonly List<Vector3> positions = new();
		private readonly List<MonsterObject> wards = new();
		private readonly List<Action> stopTelegraphs = new();
		private Stage stage = Stage.Waiting;
		private BTRunner payoff;
		private float readyTime;
		private float windowEndTime;
		// 이 시각까지 다 부수면 취소. 큰 공격이 예고를 시작하면 그 예고 끝으로 정함
		private float deadline = float.PositiveInfinity;
		private float staggerEndTime;
		private Action stopRecovery;

		public int CompletedCount { get; private set; }
		public bool IsAttacking => stage != Stage.Waiting;
		public bool WasCut { get; private set; }

		public BT_WardAttack(UnitObject unitObject, PlayerProvider playerProvider, WardMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			switch (stage)
			{
				case Stage.Waiting:
					if (CanStart() == false)
						return chase.UpdateBT();
					BeginWindow();
					return BTState.Success;
				case Stage.Window:
					if (CheckBroken())
						return BTState.Success;
					if (Time.time >= windowEndTime)
						stage = Stage.Payoff;
					return BTState.Success;
				case Stage.Payoff:
					return TickPayoff();
				default:
					if (Time.time >= staggerEndTime)
						Finish();
					return BTState.Success;
			}
		}

		private bool CanStart()
		{
			if (Time.time < readyTime || unitObject.IsActionSuspended || MonsterTemperamentDriver.HoldsAttack(unitObject))
				return false;
			Vector3 offset = playerProvider.Current.transform.position - unitObject.transform.position;
			offset.y = 0f;
			return offset.sqrMagnitude <= attack.TriggerRange * attack.TriggerRange;
		}

		private void BeginWindow()
		{
			stage = Stage.Window;
			WasCut = false;
			deadline = float.PositiveInfinity;
			windowEndTime = Time.time + attack.WindupSeconds;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();
			attack.PlayWindupPose(unitObject, attack.MinBreakSeconds);
			Vector3 center = unitObject.transform.position;
			if (attack.WindupTelegraph != null && attack.WindupSeconds > 0f)
				stopTelegraphs.Add(attack.WindupTelegraph.Play(new CombatFxContext
				{
					Position = center,
					Follow = unitObject.transform,
					Radius = attack.TelegraphRadius,
					Seconds = attack.WindupSeconds,
				}));
			positions.Clear();
			attack.WardPositions(center, playerProvider.Current.transform.position, positions);
			foreach (Vector3 position in positions)
			{
				MonsterObject ward = SummonMonsterAttack.SpawnMonster(attack.Ward, position);
				// 멀리 달아나도 생성기가 치워 공짜로 취소되지 않게
				ward.IsAnchored = true;
				wards.Add(ward);
				if (attack.WindupFx != null)
					stopTelegraphs.Add(attack.WindupFx.Play(new CombatFxContext
					{
						Position = ward.transform.position,
						Origin = center,
						Radius = attack.WardFxRadius,
						Seconds = attack.WindupSeconds,
					}));
			}
		}

		private BTState TickPayoff()
		{
			payoff ??= attack.Payoff.CreateBehaviour(unitObject, playerProvider);
			IAttackCycle cycle = (IAttackCycle)payoff;
			if (CheckBroken())
				return BTState.Success;
			BTState state = payoff.UpdateBT();
			// 큰 공격의 예고가 시작된 그 틱부터 첫 자리가 때릴 때까지가 마지막 기회
			if (cycle.IsAttacking && float.IsPositiveInfinity(deadline))
				deadline = Time.time + attack.Payoff.WindupSeconds + attack.Payoff.StrikeDelay(0);
			if (stage == Stage.Payoff && cycle.CompletedCount > 0)
			{
				WasCut = cycle.WasCut;
				ClearWards();
				Finish();
			}
			return state;
		}

		// 다 부서졌으면 큰 공격을 끊고 주저앉음
		private bool CheckBroken()
		{
			float limit = stage == Stage.Window ? float.PositiveInfinity : deadline;
			if (WardMonsterAttack.IsCancelled(CountStanding(), Time.time, limit) == false)
				return false;
			if (payoff != null)
				attack.Payoff.Interrupt(payoff);
			payoff = null;
			ClearTelegraphs();
			wards.Clear();
			stage = Stage.Staggered;
			staggerEndTime = Time.time + attack.RecoverySeconds;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Rest();
			stopRecovery = attack.PlayRecovery(unitObject);
			if (attack.BreakFx != null)
				stopRecovery += attack.BreakFx.Play(new CombatFxContext
				{
					Position = unitObject.transform.position,
					Origin = unitObject.transform.position,
					Radius = attack.RecoveryFxRadius,
					Seconds = attack.RecoverySeconds,
				});
			return true;
		}

		// 세운 물체 가운데 아직 서 있는 수. 풀에 돌아갔다 다른 자리로 다시 나온 몸은 안 셈
		private int CountStanding()
		{
			int count = 0;
			foreach (MonsterObject ward in wards)
			{
				if (ward != null && ward.gameObject.activeInHierarchy && ward.IsAlive && ward.UnitData == attack.Ward)
					count++;
			}
			return count;
		}

		private void Finish()
		{
			stopRecovery = null;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
			stage = Stage.Waiting;
			payoff = null;
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
		}

		private void ClearTelegraphs()
		{
			foreach (Action stop in stopTelegraphs)
				stop?.Invoke();
			stopTelegraphs.Clear();
		}

		// 못 부순 물체는 큰 공격 뒤에 사라짐 (풀로)
		private void ClearWards()
		{
			foreach (MonsterObject ward in wards)
			{
				if (ward != null && ward.gameObject.activeInHierarchy && ward.UnitData == attack.Ward)
					ward.gameObject.SetActive(false);
			}
			wards.Clear();
		}

		// 끊김 (사망, 풀 반납, 단계 이동). 예고, 큰 공격, 물체, 반격 창을 치움. 끝난 뒤 불러도 무해
		public void Interrupt()
		{
			if (payoff != null)
				attack.Payoff.Interrupt(payoff);
			payoff = null;
			ClearTelegraphs();
			ClearWards();
			stopRecovery?.Invoke();
			stopRecovery = null;
			if (unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
			stage = Stage.Waiting;
		}
	}
}
