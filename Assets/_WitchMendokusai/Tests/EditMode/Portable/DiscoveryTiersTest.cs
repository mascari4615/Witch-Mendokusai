using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai.Tests
{
	/// <summary>계단 보상 셈. 문턱마다 한 계단, 문턱 사이는 평평, 문턱 0 은 계단 없음</summary>
	public class DiscoveryTiersTest
	{
		[Test]
		public void BelowTheFirstThreshold_NoStep()
		{
			Assert.AreEqual(0, DiscoveryTiers.StepsOf(4, 5));
			Assert.AreEqual(1d, DiscoveryTiers.MultiplierOf(4, 5, 0.15d), 1e-12d);
		}

		[Test]
		public void EachThreshold_OneStep()
		{
			Assert.AreEqual(1, DiscoveryTiers.StepsOf(5, 5));
			Assert.AreEqual(1, DiscoveryTiers.StepsOf(9, 5));
			Assert.AreEqual(2, DiscoveryTiers.StepsOf(10, 5));
			Assert.AreEqual(1d + 2 * 0.15d, DiscoveryTiers.MultiplierOf(10, 5, 0.15d), 1e-12d);
		}

		[Test]
		public void ZeroThreshold_MeansNoStairs()
		{
			Assert.AreEqual(0, DiscoveryTiers.StepsOf(100, 0));
			Assert.AreEqual(1d, DiscoveryTiers.MultiplierOf(100, 0, 0.15d), 1e-12d);
		}

		[Test]
		public void NegativeScore_NoStep()
		{
			Assert.AreEqual(0, DiscoveryTiers.StepsOf(-7, 5));
		}
	}
}
