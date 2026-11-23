using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class AttackSpeedTierRulesTests
	{
		private static readonly float[] THRESHOLDS = { 0.95f, 0.65f, 0.45f };

		[Test]
		public void 간격이_문턱을_넘을_때마다_단계가_오른다()
		{
			Assert.AreEqual(0, AttackSpeedTierRules.Tier(1.3f, THRESHOLDS));
			Assert.AreEqual(1, AttackSpeedTierRules.Tier(0.95f, THRESHOLDS));
			Assert.AreEqual(1, AttackSpeedTierRules.Tier(0.7f, THRESHOLDS));
			Assert.AreEqual(2, AttackSpeedTierRules.Tier(0.65f, THRESHOLDS));
			Assert.AreEqual(3, AttackSpeedTierRules.Tier(0.325f, THRESHOLDS), "바닥 (기본 1.3초의 25%) 은 최상 단계");
		}

		[Test]
		public void 길이는_카드만큼_늘고_최상_단계의_화면_끝은_더_길다()
		{
			Assert.AreEqual(1f, AttackSpeedTierRules.LengthMultiplier(0, 0.1f, false, 3.5f, 3, 3), 0.0001f);
			Assert.AreEqual(1.5f, AttackSpeedTierRules.LengthMultiplier(5, 0.1f, false, 3.5f, 3, 3), 0.0001f);
			Assert.AreEqual(1.5f, AttackSpeedTierRules.LengthMultiplier(5, 0.1f, true, 3.5f, 2, 3), 0.0001f, "최상 단계 전에는 화면 끝 아님");
			Assert.AreEqual(3.5f, AttackSpeedTierRules.LengthMultiplier(5, 0.1f, true, 3.5f, 3, 3), 0.0001f);
		}

		[Test]
		public void 가속_쌓임은_상한에서_자른다()
		{
			Assert.AreEqual(0, AttackSpeedTierRules.ClampStacks(-1, 15));
			Assert.AreEqual(7, AttackSpeedTierRules.ClampStacks(7, 15));
			Assert.AreEqual(15, AttackSpeedTierRules.ClampStacks(40, 15));
		}
	}
}
