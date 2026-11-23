using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class LevelGrowthRulesTests
	{
		[Test]
		public void 카드를_받기_전에는_아무것도_안_준다()
		{
			Assert.AreEqual(0, LevelGrowthRules.Grant(0, 0, 1, 12));
			Assert.AreEqual(0, LevelGrowthRules.Grant(0, 1, 0, 12));
		}

		[Test]
		public void 받은_장수만큼_레벨업마다_준다()
		{
			Assert.AreEqual(1, LevelGrowthRules.Grant(0, 1, 1, 12));
			Assert.AreEqual(6, LevelGrowthRules.Grant(0, 2, 3, 30));
		}

		[Test]
		public void 상한에서_멈추고_남은_만큼만_준다()
		{
			Assert.AreEqual(2, LevelGrowthRules.Grant(10, 1, 3, 12));
			Assert.AreEqual(0, LevelGrowthRules.Grant(12, 1, 1, 12));
			Assert.AreEqual(0, LevelGrowthRules.Grant(15, 1, 1, 12), "넘친 값도 0");
		}

		[Test]
		public void 상한_0_은_끝없이_준다()
		{
			Assert.AreEqual(4, LevelGrowthRules.Grant(999, 1, 4, 0));
		}
	}
}
