using System;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai.Tests
{
	/// <summary>기록 단계. 쌓인 값이 목표 표의 몇 번째까지 닿았나, 잘못된 표는 던짐</summary>
	public class DiscoveryStagesTest
	{
		private static readonly int[] TARGETS = { 1, 20, 100, 300, 1000 };

		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(19, 1)]
		[TestCase(20, 2)]
		[TestCase(999, 4)]
		[TestCase(1000, 5)]
		[TestCase(99999, 5)]
		public void 닿은_가장_높은_단계(int value, int expected)
		{
			Assert.AreEqual(expected, DiscoveryStages.Resolve(value, TARGETS));
		}

		[Test]
		public void 잘못된_목표_표는_던진다()
		{
			Assert.Throws<ArgumentException>(() => DiscoveryStages.Validate("null", null));
			Assert.Throws<ArgumentException>(() => DiscoveryStages.Validate("empty", new int[0]));
			Assert.Throws<ArgumentException>(() => DiscoveryStages.Validate("zero", new[] { 0, 10 }));
			Assert.Throws<ArgumentException>(() => DiscoveryStages.Validate("same", new[] { 10, 10 }));
			Assert.DoesNotThrow(() => DiscoveryStages.Validate("ok", TARGETS));
		}
	}
}
