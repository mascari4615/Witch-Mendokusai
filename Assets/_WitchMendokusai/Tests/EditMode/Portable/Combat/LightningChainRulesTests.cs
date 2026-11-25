using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class LightningChainRulesTests
	{
		private static readonly LightningPoint origin = new(0f, 0f);
		private readonly List<LightningHop> hops = new();

		private static List<LightningPoint> Line(params float[] xs)
		{
			List<LightningPoint> points = new();
			foreach (float x in xs)
				points.Add(new LightningPoint(x, 0f));
			return points;
		}

		[Test]
		public void ChainHopsFromFoeToFoe()
		{
			List<LightningPoint> foes = Line(8f, 2f, 5f);
			LightningChainRules.Chain(origin, foes, new bool[3], 3, 3.5f, hops);
			Assert.AreEqual(3, hops.Count, "사거리 3.5 씩 건너 8 까지 닿음");
			Assert.AreEqual(LightningChainRules.NO_POINT, hops[0].From);
			Assert.AreEqual(1, hops[0].To);
			Assert.AreEqual(1, hops[1].From);
			Assert.AreEqual(2, hops[1].To);
			Assert.AreEqual(0, hops[2].To);
		}

		[Test]
		public void BurstReachesOnlyFromOrigin()
		{
			List<LightningPoint> foes = Line(8f, 2f, 5f);
			LightningChainRules.Burst(origin, foes, new bool[3], 5, 6f, hops);
			Assert.AreEqual(2, hops.Count);
			foreach (LightningHop hop in hops)
				Assert.AreEqual(LightningChainRules.NO_POINT, hop.From);
			Assert.AreEqual(1, hops[0].To, "가까운 적부터");
			Assert.AreEqual(2, hops[1].To);
		}

		[Test]
		public void BlastVictimsAreNotHitAgain()
		{
			List<LightningPoint> foes = Line(1f, 2f);
			LightningChainRules.Burst(origin, foes, new[] { true, false }, 5, 6f, hops);
			Assert.AreEqual(1, hops.Count);
			Assert.AreEqual(1, hops[0].To);
		}

		[Test]
		public void SameFoeIsNeverUsedTwiceAcrossCalls()
		{
			List<LightningPoint> foes = Line(1f, 2f, 3f);
			bool[] used = new bool[3];
			LightningChainRules.Chain(origin, foes, used, 2, 5f, hops);
			LightningChainRules.Burst(origin, foes, used, 5, 5f, hops);
			Assert.AreEqual(1, hops.Count, "이어짐이 쓴 둘을 뺀 하나");
			Assert.AreEqual(2, hops[0].To);
		}

		[Test]
		public void HopLimitAndEmptyField()
		{
			LightningChainRules.Chain(origin, Line(1f, 2f, 3f), new bool[3], 0, 5f, hops);
			Assert.AreEqual(0, hops.Count);
			LightningChainRules.Chain(origin, Line(), new bool[0], 3, 5f, hops);
			Assert.AreEqual(0, hops.Count);
		}

		[Test]
		public void RangeEdgeCountsAndTieGoesToTheFirstFoe()
		{
			LightningChainRules.Chain(origin, Line(5f, -5f), new bool[2], 1, 5f, hops);
			Assert.AreEqual(1, hops.Count);
			Assert.AreEqual(0, hops[0].To);
		}

		[Test]
		public void DamageIsAtLeastOne()
		{
			Assert.AreEqual(24, LightningChainRules.Damage(60, 40));
			Assert.AreEqual(1, LightningChainRules.Damage(1, 40));
			Assert.AreEqual(0, LightningChainRules.Damage(0, 40));
			Assert.AreEqual(0, LightningChainRules.Damage(60, 0));
		}
	}
}
