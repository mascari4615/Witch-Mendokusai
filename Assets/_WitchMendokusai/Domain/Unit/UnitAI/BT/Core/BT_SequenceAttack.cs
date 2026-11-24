using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 공격 순서 (SequenceMonsterAttack). 낱개 공격의 행동을 하나씩 새로 만들어 돌리고, 한 번 끝나면 다음으로
	public class BT_SequenceAttack : BTRunner, IAttackCycle
	{
		private readonly SequenceMonsterAttack attack;
		private readonly BT_MoveToPlayer chase;
		private BTRunner step;
		private int stepIndex;
		private float readyTime;

		public int CompletedCount { get; private set; }
		public bool IsAttacking => step is IAttackCycle { IsAttacking: true };

		public BT_SequenceAttack(UnitObject unitObject, PlayerProvider playerProvider, SequenceMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			if (step == null && Time.time < readyTime)
				return chase.UpdateBT();
			step ??= attack.Steps[stepIndex].CreateBehaviour(unitObject, playerProvider);
			// 공격 도중 반격으로 죽으면 Interrupt 가 step 을 비움
			BTRunner running = step;
			BTState state = running.UpdateBT();
			if (step == running && ((IAttackCycle)running).CompletedCount > 0)
				Advance();
			return state;
		}

		private void Advance()
		{
			step = null;
			stepIndex++;
			if (stepIndex < attack.Steps.Count)
				return;
			stepIndex = 0;
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
		}

		// 남은 예고를 치움
		public void Interrupt()
		{
			if (step != null)
				attack.Steps[stepIndex].Interrupt(step);
			step = null;
		}
	}
}
