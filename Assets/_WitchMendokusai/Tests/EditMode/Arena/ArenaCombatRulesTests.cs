using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// ArenaCombatRules.ShouldDamage 적아판정 회귀. 순수 함수라 MonoBehaviour/물리 없이 검증.
	/// WM-165 item 3 — 아레나 팀 판정 + 레거시(도시/던전) 2진 판정 무변경 동시 보장.
	/// </summary>
	public class ArenaCombatRulesTests
	{
		// --- 아레나 경로: 팀 비교(다대다) ---

		[Test]
		public void Arena_DifferentTeam_Damages()
		{
			Assert.IsTrue(ArenaCombatRules.ShouldDamage(true, 0, true, 1, false, VictimKind.Other));
		}

		[Test]
		public void Arena_SameTeam_NoFriendlyFire()
		{
			Assert.IsFalse(ArenaCombatRules.ShouldDamage(true, 0, true, 0, false, VictimKind.Other));
		}

		// --- 레거시 폴백(비-아레나): 기존 usedByPlayer 2진 판정 무변경 ---

		[Test]
		public void Legacy_PlayerSkill_HitsMonster()
		{
			Assert.IsTrue(ArenaCombatRules.ShouldDamage(false, -1, false, -1, true, VictimKind.Monster));
		}

		[Test]
		public void Legacy_PlayerSkill_HitsResourceNode()
		{
			Assert.IsTrue(ArenaCombatRules.ShouldDamage(false, -1, false, -1, true, VictimKind.ResourceNode));
		}

		[Test]
		public void Legacy_PlayerSkill_DoesNotHitPlayer()
		{
			Assert.IsFalse(ArenaCombatRules.ShouldDamage(false, -1, false, -1, true, VictimKind.Player));
		}

		[Test]
		public void Legacy_MonsterSkill_HitsPlayer()
		{
			Assert.IsTrue(ArenaCombatRules.ShouldDamage(false, -1, false, -1, false, VictimKind.Player));
		}

		[Test]
		public void Legacy_MonsterSkill_DoesNotHitMonster()
		{
			Assert.IsFalse(ArenaCombatRules.ShouldDamage(false, -1, false, -1, false, VictimKind.Monster));
		}

		// --- 혼합 안전: 한쪽만 아레나면 레거시 폴백 ---

		[Test]
		public void OnlyOwnerInArena_FallsBackToLegacy()
		{
			Assert.IsTrue(ArenaCombatRules.ShouldDamage(true, 0, false, -1, true, VictimKind.Monster));
		}
	}
}
