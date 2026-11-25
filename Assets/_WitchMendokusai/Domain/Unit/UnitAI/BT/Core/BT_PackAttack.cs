using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 무리면 무리 공격, 혼자면 거리를 두고 떠돎 (PackMonsterAttack). 무리 공격의 행동은 흩어져도 남겨 대기 시간을 잇음
	public class BT_PackAttack : BTRunner
	{
		private readonly PackMonsterAttack attack;
		private BTRunner groupBehaviour;
		private BTRunner soloBehaviour;
		private float nextCountTime;
		private bool isGrouped;

		public BT_PackAttack(UnitObject unitObject, PlayerProvider playerProvider, PackMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			if (Time.time >= nextCountTime)
			{
				nextCountTime = Time.time + attack.CountInterval;
				// 시작한 무리 공격은 끝까지. 하나가 죽어도 예고가 중간에 사라지지 않게
				if (groupBehaviour is not IAttackCycle { IsAttacking: true })
					isGrouped = attack.IsGrouped(unitObject);
			}
			if (isGrouped == false)
				return Lure();
			groupBehaviour ??= attack.GroupAttack.CreateBehaviour(unitObject, playerProvider);
			return groupBehaviour.UpdateBT();
		}

		// 혼자 공격이 있고 주인공이 그 사거리까지 다가오면 침. 시작한 공격은 끝까지, 끝나면 다시 거리를 둠
		private bool UsesSoloAttack()
		{
			if (attack.SoloAttack == null)
				return false;
			if (soloBehaviour is IAttackCycle { IsAttacking: true })
				return true;
			return SlimeKingPatternMath.IsWithin(unitObject.transform.position, playerProvider.Current.transform.position, attack.SoloAttack.TriggerRange);
		}

		private BTState Lure()
		{
			if (UsesSoloAttack())
			{
				soloBehaviour ??= attack.SoloAttack.CreateBehaviour(unitObject, playerProvider);
				BTRunner running = soloBehaviour;
				return running.UpdateBT();
			}
			Vector3 direction = PackMonsterAttack.LureDirection(unitObject.transform.position, playerProvider.Current.transform.position, attack.LureDistance, attack.LureBand);
			unitObject.UnitMovement.SetMoveDirection(direction);
			return BTState.Success;
		}

		public void Interrupt()
		{
			if (groupBehaviour != null)
				attack.GroupAttack.Interrupt(groupBehaviour);
			groupBehaviour = null;
			if (soloBehaviour != null)
				attack.SoloAttack.Interrupt(soloBehaviour);
			soloBehaviour = null;
		}
	}
}
