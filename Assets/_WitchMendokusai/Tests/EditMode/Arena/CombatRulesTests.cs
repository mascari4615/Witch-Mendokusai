using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// CombatRules.ShouldDamage 적아판정 회귀. 순수 함수라 MonoBehaviour/물리 없이 검증.
	/// WM-165 item 3 — 매치(투기장·개척) 팀 판정 + 레거시(도시/던전) 2진 판정 무변경 동시 보장.
	/// </summary>
	public class CombatRulesTests
	{
		// --- 매치 경로: 팀 비교(다대다) ---

		[Test]
		public void Match_DifferentTeam_Damages()
		{
			Assert.IsTrue(CombatRules.ShouldDamage(true, 0, true, 1, false, VictimKind.Other));
		}

		[Test]
		public void Match_SameTeam_NoFriendlyFire()
		{
			Assert.IsFalse(CombatRules.ShouldDamage(true, 0, true, 0, false, VictimKind.Other));
		}

		// --- 레거시 폴백(비-매치): 기존 usedByPlayer 2진 판정 무변경 ---

		[Test]
		public void Legacy_PlayerSkill_HitsMonster()
		{
			Assert.IsTrue(CombatRules.ShouldDamage(false, -1, false, -1, true, VictimKind.Monster));
		}

		[Test]
		public void Legacy_PlayerSkill_HitsResourceNode()
		{
			Assert.IsTrue(CombatRules.ShouldDamage(false, -1, false, -1, true, VictimKind.ResourceNode));
		}

		[Test]
		public void Legacy_PlayerSkill_DoesNotHitPlayer()
		{
			Assert.IsFalse(CombatRules.ShouldDamage(false, -1, false, -1, true, VictimKind.Player));
		}

		[Test]
		public void Legacy_MonsterSkill_HitsPlayer()
		{
			Assert.IsTrue(CombatRules.ShouldDamage(false, -1, false, -1, false, VictimKind.Player));
		}

		[Test]
		public void Legacy_MonsterSkill_DoesNotHitMonster()
		{
			Assert.IsFalse(CombatRules.ShouldDamage(false, -1, false, -1, false, VictimKind.Monster));
		}

		// --- 혼합 안전: 한쪽만 매치 참가자면 레거시 폴백 ---

		[Test]
		public void OnlyOwnerInMatch_FallsBackToLegacy()
		{
			Assert.IsTrue(CombatRules.ShouldDamage(true, 0, false, -1, true, VictimKind.Monster));
		}
	}
}
