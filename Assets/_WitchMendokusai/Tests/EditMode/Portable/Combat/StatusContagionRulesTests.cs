using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class StatusContagionRulesTests
	{
		[Test]
		public void GenerationCapStopsChain()
		{
			Assert.IsTrue(StatusContagionRules.CanSpread(0, 3));
			Assert.IsTrue(StatusContagionRules.CanSpread(2, 3));
			Assert.IsFalse(StatusContagionRules.CanSpread(3, 3));
			Assert.IsFalse(StatusContagionRules.CanSpread(0, 0));
			Assert.AreEqual(2, StatusContagionRules.NextGeneration(1));
		}

		[Test]
		public void ReceiveCapStopsThatUnit()
		{
			Assert.IsTrue(StatusContagionRules.CanReceive(2, 3));
			Assert.IsFalse(StatusContagionRules.CanReceive(3, 3));
		}

		[Test]
		public void SelectsNearestWithinRadiusAndLimit()
		{
			List<int> result = new();
			float[] squared = { 4f, 1f, 100f, 2.25f };
			int[] received = { 0, 0, 0, 0 };
			StatusContagionRules.SelectTargets(squared, received, 2.5f, 2, 3, result);
			CollectionAssert.AreEqual(new[] { 1, 3 }, result);
		}

		[Test]
		public void SkipsUnitsThatReachedReceiveCap()
		{
			List<int> result = new();
			float[] squared = { 1f, 2f, 3f };
			int[] received = { 3, 0, 1 };
			StatusContagionRules.SelectTargets(squared, received, 2.5f, 5, 3, result);
			CollectionAssert.AreEqual(new[] { 1, 2 }, result);
		}

		[Test]
		public void TiesKeepIndexOrderAndZeroRadiusSelectsNone()
		{
			List<int> result = new();
			float[] squared = { 1f, 1f };
			StatusContagionRules.SelectTargets(squared, null, 2.5f, 2, 3, result);
			CollectionAssert.AreEqual(new[] { 0, 1 }, result);
			StatusContagionRules.SelectTargets(squared, null, 0f, 2, 3, result);
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		public void ChainSimulationEndsAtGenerationCap()
		{
			// 직접 중독 0세대, 죽을 때마다 다음 세대로. 상한 3이면 세대 3 에서 멈춤
			int generation = 0;
			int infected = 1;
			while (StatusContagionRules.CanSpread(generation, 3))
			{
				generation = StatusContagionRules.NextGeneration(generation);
				infected++;
			}
			Assert.AreEqual(4, infected);
			Assert.AreEqual(3, generation);
		}
	}
}
