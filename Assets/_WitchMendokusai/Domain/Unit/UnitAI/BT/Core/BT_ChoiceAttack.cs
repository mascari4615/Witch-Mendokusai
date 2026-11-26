using System;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 갈래 선택 (ChoiceMonsterAttack). 대기 동안 추적 -> 사거리 안이면 멈추고 갈래 하나를 골라 단서 (물체, 몸 색) 를 보임 -> 단서 초 뒤 갈래 공격을 한 번 돌림 -> 대기
	// 갈래 공격의 행동은 고를 때마다 새로 만들어 상태가 이월되지 않게 함
	public class BT_ChoiceAttack : BTRunner, IAttackCycle
	{
		private const int NOT_CHOSEN = -1;

		private readonly ChoiceMonsterAttack attack;
		private readonly BT_MoveToPlayer chase;
		private readonly List<Vector3> offsets = new();
		private readonly List<Action> stopCues = new();
		private BTRunner branch;
		private int branchIndex = NOT_CHOSEN;
		private float readyTime;
		private float cueEndTime;
		// 몸 색을 되돌릴 시각. 단서 물체와 같이 갈래 예고가 끝날 때
		private float tintEndTime;
		private Color savedColor;
		private bool isTinted;

		public int CompletedCount { get; private set; }
		public bool IsAttacking => branchIndex != NOT_CHOSEN;
		public bool WasCut { get; private set; }

		public BT_ChoiceAttack(UnitObject unitObject, PlayerProvider playerProvider, ChoiceMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			if (branchIndex == NOT_CHOSEN)
			{
				if (CanStart() == false)
					return chase.UpdateBT();
				BeginCue(attack.PickBranch(UnityEngine.Random.value));
			}
			// 단서 동안은 멈춰서 보이기만. 기절, 빙결이면 끊긴 것으로 끝
			if (Time.time < cueEndTime)
			{
				if (unitObject.IsActionSuspended)
					Finish(true);
				return BTState.Success;
			}
			if (isTinted && Time.time >= tintEndTime)
				RestoreTint();
			branch ??= attack.Branches[branchIndex].Attack.CreateBehaviour(unitObject, playerProvider);
			BTRunner running = branch;
			BTState state = running.UpdateBT();
			// 반격으로 죽으면 Interrupt 가 비움
			if (branch == running && ((IAttackCycle)running).CompletedCount > 0)
				Finish(((IAttackCycle)running).WasCut);
			return state;
		}

		private bool CanStart()
		{
			if (Time.time < readyTime || unitObject.IsActionSuspended || MonsterTemperamentDriver.HoldsAttack(unitObject))
				return false;
			Vector3 offset = playerProvider.Current.transform.position - unitObject.transform.position;
			offset.y = 0f;
			return offset.sqrMagnitude <= attack.TriggerRange * attack.TriggerRange;
		}

		private void BeginCue(int index)
		{
			branchIndex = index;
			branch = null;
			ChoiceBranch chosen = attack.Branches[index];
			cueEndTime = Time.time + attack.CueSeconds;
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
			unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();
			// 몸짓은 갈래와 상관없이 같음. 단서만 다름
			attack.PlayWindupPose(unitObject, attack.CueSeconds);
			// 단서는 갈래 공격의 예고가 끝날 때까지
			float cueSeconds = attack.CueSeconds + chosen.Attack.WindupSeconds;
			tintEndTime = Time.time + cueSeconds;
			offsets.Clear();
			attack.CueOffsets(chosen.CueCount, offsets);
			Vector3 center = unitObject.transform.position;
			foreach (Vector3 offset in offsets)
			{
				stopCues.Add(chosen.CueFx.Play(new CombatFxContext
				{
					Position = center + offset,
					Origin = center,
					Direction = offset.sqrMagnitude > 0f ? offset.normalized : Vector3.forward,
					Radius = attack.CueRadius,
					Seconds = cueSeconds,
				}));
			}
			if (chosen.BodyTint != Color.white)
			{
				savedColor = unitObject.SpriteRenderer.color;
				unitObject.SpriteRenderer.color = savedColor * chosen.BodyTint;
				isTinted = true;
			}
		}

		private void Finish(bool wasCut)
		{
			WasCut = wasCut;
			ClearCue();
			CompletedCount++;
			branchIndex = NOT_CHOSEN;
			branch = null;
			readyTime = Time.time + attack.Cooldown;
		}

		private void ClearCue()
		{
			foreach (Action stop in stopCues)
				stop?.Invoke();
			stopCues.Clear();
			RestoreTint();
			// 갈래 공격이 아직 안 시작했으면 단서 몸짓을 여기서 풂 (시작했으면 갈래가 풂)
			if (branch == null && unitObject.TryGetComponent(out MonsterPoseDriver pose))
				pose.Release();
		}

		// 단서 동안 바꾼 몸 색만 되돌림. 그동안 정예나 특징이 색을 바꿨으면 그 색은 덮임 (단서 초 동안만)
		private void RestoreTint()
		{
			if (isTinted)
				unitObject.SpriteRenderer.color = savedColor;
			isTinted = false;
		}

		// 끊김 (사망, 풀 반납, 패턴 이동). 단서와 갈래 예고를 치움, 끝난 뒤 불러도 무해
		public void Interrupt()
		{
			if (branch != null)
				attack.Branches[branchIndex].Attack.Interrupt(branch);
			branch = null;
			ClearCue();
			branchIndex = NOT_CHOSEN;
		}
	}
}
