using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 체력 단계 (PhaseMonsterAttack). 하던 공격 (숨 고르기까지) 을 끝낸 뒤 단계를 바꾸고, 들어오는 공격이 있으면 한 번 한 다음 새 공격의 행동을 새로 만듦
	public class BT_PhaseAttack : BTRunner, IAttackCycle
	{
		private readonly PhaseMonsterAttack attack;
		private BTRunner behaviour;
		// behaviour 를 만든 공격. 들어오는 공격이거나 지금 단계의 공격
		private MonsterAttack active;
		private int phaseIndex;
		// 등장 멈춤이 끝나는 시각. 첫 틱에 정함
		private float openingEndTime = -1f;
		// 이 단계의 전환 공격을 다시 했음
		private bool hasRetried;

		public int CompletedCount => behaviour is IAttackCycle cycle ? cycle.CompletedCount : 0;
		public bool IsAttacking => behaviour is IAttackCycle { IsAttacking: true };

		public BT_PhaseAttack(UnitObject unitObject, PlayerProvider playerProvider, PhaseMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			float ratio = (float)unitObject.UnitStat[UnitStatType.HP_CUR] / unitObject.UnitStat[UnitStatType.HP_MAX];
			if (openingEndTime < 0f)
			{
				openingEndTime = Time.time + (ratio >= 1f ? attack.OpeningSeconds : 0f);
				// 재배치 뒤 다시 시작한 것이면 들어오는 공격 없이 그 단계에서 이어감
				phaseIndex = attack.PhaseIndex(ratio, 0);
				ApplyBody();
			}
			if (Time.time < openingEndTime)
			{
				unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
				unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();
				return BTState.Running;
			}
			int next = attack.PhaseIndex(ratio, phaseIndex);
			if (next != phaseIndex && IsAttacking == false)
			{
				Interrupt();
				phaseIndex = next;
				hasRetried = false;
				ApplyBody();
				Begin(attack.Phases[next].Entrance);
			}
			if (IsEntranceOver())
			{
				// 기절로 끊겼으면 전환을 한 번 더. 끊기기 전에 남긴 물체는 식힘
				bool wasCut = behaviour is IAttackCycle { WasCut: true } && hasRetried == false;
				Interrupt();
				if (wasCut)
				{
					hasRetried = true;
					LingerField.Abandon(unitObject);
					Begin(attack.Phases[phaseIndex].Entrance);
				}
			}
			if (behaviour == null)
				Begin(attack.Phases[phaseIndex].Attack);
			// 공격 도중 반격으로 죽으면 Interrupt 가 behaviour 를 비움
			BTRunner running = behaviour;
			return running.UpdateBT();
		}

		private bool IsEntranceOver() => active != null && active == attack.Phases[phaseIndex].Entrance && CompletedCount > 0;

		private void Begin(MonsterAttack next)
		{
			if (next == null)
				return;
			active = next;
			behaviour = next.CreateBehaviour(unitObject, playerProvider);
		}

		private void ApplyBody()
		{
			Sprite sprite = attack.Phases[phaseIndex].BodySprite;
			if (sprite != null)
				MonsterPoseDriver.Of(unitObject).SetBase(sprite);
		}

		public void Interrupt()
		{
			if (behaviour != null)
				active.Interrupt(behaviour);
			behaviour = null;
			active = null;
		}
	}
}
