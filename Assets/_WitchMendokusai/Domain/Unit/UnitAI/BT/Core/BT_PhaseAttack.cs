using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 체력 단계 (PhaseMonsterAttack). 하던 공격 (숨 고르기까지) 을 끝낸 뒤 단계를 바꾸고 새 공격의 행동을 새로 만듦
	public class BT_PhaseAttack : BTRunner, IAttackCycle
	{
		private readonly PhaseMonsterAttack attack;
		private BTRunner behaviour;
		private int phaseIndex;

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
			int next = attack.PhaseIndex(ratio, phaseIndex);
			if (next != phaseIndex && IsAttacking == false)
			{
				Interrupt();
				phaseIndex = next;
			}
			behaviour ??= attack.Phases[phaseIndex].Attack.CreateBehaviour(unitObject, playerProvider);
			// 공격 도중 반격으로 죽으면 Interrupt 가 behaviour 를 비움
			BTRunner running = behaviour;
			return running.UpdateBT();
		}

		public void Interrupt()
		{
			if (behaviour != null)
				attack.Phases[phaseIndex].Attack.Interrupt(behaviour);
			behaviour = null;
		}
	}
}
