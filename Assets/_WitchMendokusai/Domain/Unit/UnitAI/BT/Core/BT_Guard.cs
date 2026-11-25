using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 웅크리기의 박자 (GuardMonsterAttack). 방어력을 더하고, 끝나거나 끊기면 더한 만큼만 되돌림
	public class BT_Guard : BT_StancedAttack
	{
		private readonly GuardMonsterAttack attack;
		private int addedArmor;
		private Action stopGuard;

		public BT_Guard(UnitObject unitObject, PlayerProvider playerProvider, GuardMonsterAttack attack) : base(unitObject, playerProvider, attack)
		{
			this.attack = attack;
		}

		protected override float StanceSeconds => attack.GuardSeconds;

		protected override void OnStanceBegin()
		{
			addedArmor = attack.Armor;
			unitObject.UnitStat[UnitStatType.ARMOR] += addedArmor;
			if (attack.GuardFx == null)
				return;
			stopGuard = attack.GuardFx.Play(new CombatFxContext
			{
				Position = unitObject.transform.position,
				Follow = unitObject.transform,
				Radius = attack.GuardFxRadius,
				Seconds = attack.GuardSeconds,
			});
		}

		protected override void OnStanceEnd(bool interrupted)
		{
			// 끝까지 갔으면 표시가 같은 길이라 스스로 마무리
			if (interrupted)
				stopGuard?.Invoke();
			stopGuard = null;
			unitObject.UnitStat[UnitStatType.ARMOR] -= addedArmor;
			addedArmor = 0;
		}
	}
}
