using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class LightningChainRulesTests
	{
		private readonly List<LightningHop> hops = new();

		private static List<LightningTarget> Line(params float[] xs)
		{
			List<LightningTarget> targets = new();
			foreach (float x in xs)
				targets.Add(new LightningTarget(x, 0f, false));
			return targets;
		}

		[Test]
		public void ChainHopsFromFoeToFoe()
		{
			LightningChainRules.Chain(0f, 0f, 0f, Line(8f, 2f, 5f), 3, 3.5f, hops);
			Assert.AreEqual(3, hops.Count, "사거리 3.5 씩 건너 8 까지 닿음");
			Assert.AreEqual(LightningChainRules.BLAST, hops[0].From);
			Assert.AreEqual(1, hops[0].To);
			Assert.AreEqual(1, hops[1].From);
			Assert.AreEqual(2, hops[1].To);
			Assert.AreEqual(0, hops[2].To);
		}

		[Test]
		public void ChainMeasuresFirstHopFromBlastEdge()
		{
			LightningChainRules.Chain(0f, 0f, 2f, Line(4f), 3, 2f, hops);
			Assert.AreEqual(1, hops.Count, "폭발 반경 2 밖으로 사거리 2");
			LightningChainRules.Chain(0f, 0f, 2f, Line(4f, 8f), 3, 2f, hops);
			Assert.AreEqual(1, hops.Count, "두 번째 줄은 사거리 2 만");
		}

		[Test]
		public void BurstReachesOnlyFromBlast()
		{
			LightningChainRules.Burst(0f, 0f, Line(8f, 2f, 5f), 5, 6f, hops);
			Assert.AreEqual(2, hops.Count);
			foreach (LightningHop hop in hops)
				Assert.AreEqual(LightningChainRules.BLAST, hop.From);
			Assert.AreEqual(1, hops[0].To, "가까운 적부터");
			Assert.AreEqual(2, hops[1].To);
		}

		[Test]
		public void FreshFoesComeBeforeStruckOnes()
		{
			List<LightningTarget> targets = new() { new(1f, 0f, true), new(5f, 0f, false), new(2f, 0f, true) };
			LightningChainRules.Burst(0f, 0f, targets, 2, 6f, hops);
			Assert.AreEqual(1, hops[0].To, "멀어도 안 맞은 적 먼저");
			Assert.AreEqual(0, hops[1].To, "모자라면 맞은 적");
		}

		[Test]
		public void PackInsideBlastStillGetsLightning()
		{
			List<LightningTarget> targets = new() { new(1f, 0f, true), new(2f, 0f, true) };
			LightningChainRules.Burst(0f, 0f, targets, 5, 6f, hops);
			Assert.AreEqual(2, hops.Count);
			LightningChainRules.Chain(0f, 0f, 2f, targets, 3, 2f, hops);
			Assert.AreEqual(2, hops.Count, "같은 적을 두 번 고르지 않음");
		}

		[Test]
		public void LimitsAndEmptyField()
		{
			LightningChainRules.Chain(0f, 0f, 1f, Line(1f, 2f, 3f), 0, 5f, hops);
			Assert.AreEqual(0, hops.Count);
			LightningChainRules.Chain(0f, 0f, 1f, Line(), 3, 5f, hops);
			Assert.AreEqual(0, hops.Count);
			LightningChainRules.Burst(0f, 0f, Line(3f), 5, -5f, hops);
			Assert.AreEqual(0, hops.Count, "음수 사거리는 아무도 안 닿음");
		}

		[Test]
		public void RangeEdgeCountsAndTieGoesToTheFirstFoe()
		{
			LightningChainRules.Burst(0f, 0f, Line(5f, -5f), 1, 5f, hops);
			Assert.AreEqual(1, hops.Count);
			Assert.AreEqual(0, hops[0].To);
		}

		[Test]
		public void DamageRoundsHalfUpAndIsAtLeastOne()
		{
			Assert.AreEqual(24, LightningChainRules.Damage(60, 40));
			Assert.AreEqual(13, LightningChainRules.Damage(25, 50), "반은 올림");
			Assert.AreEqual(3, LightningChainRules.Damage(5, 55));
			Assert.AreEqual(1, LightningChainRules.Damage(1, 40));
			Assert.AreEqual(0, LightningChainRules.Damage(0, 40));
			Assert.AreEqual(0, LightningChainRules.Damage(60, 0));
			Assert.AreEqual(24000000, LightningChainRules.Damage(60000000, 40));
		}
	}
}
