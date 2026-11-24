using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 무리면 무리 공격, 혼자면 거리를 두고 떠돎 (PackMonsterAttack). 무리 공격의 행동은 흩어져도 남겨 대기 시간을 잇음
	public class BT_PackAttack : BTRunner
	{
		private readonly PackMonsterAttack attack;
		private BTRunner groupBehaviour;
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

		private BTState Lure()
		{
			Vector3 direction = PackMonsterAttack.LureDirection(unitObject.transform.position, playerProvider.Current.transform.position, attack.LureDistance, attack.LureBand);
			unitObject.UnitMovement.SetMoveDirection(direction);
			return BTState.Success;
		}

		public void Interrupt()
		{
			if (groupBehaviour != null)
				attack.GroupAttack.Interrupt(groupBehaviour);
			groupBehaviour = null;
		}
	}
}
