using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class BusDuckTest
	{
		[TestCase(0f, -12f, 0.075f, -6f)]
		[TestCase(-6f, -12f, 1f, -12f)]
		[TestCase(-12f, 0f, 0.075f, -6f)]
		public void StepCrossesFullRangeInFadeSeconds(float currentDb, float targetDb, float deltaTime, float expectedDb)
			=> Assert.AreEqual(expectedDb, BusDuck.Step(currentDb, targetDb, -12f, 0.15f, deltaTime), 0.0001f);

		[Test]
		public void ZeroFadeJumpsToTarget()
			=> Assert.AreEqual(-12f, BusDuck.Step(0f, -12f, -12f, 0f, 0.001f), 0.0001f);

		[TestCase(0f, 1f)]
		[TestCase(-6f, 0.5012f)]
		public void LinearFromDecibels(float decibels, float expected)
			=> Assert.AreEqual(expected, BusDuck.ToLinear(decibels), 0.001f);

		// AudioManager 가 잡는 전투 버스가 FMOD 프로젝트에 있는지 (뱅크와 경로가 어긋나면 오디오 전체가 무음으로 빠짐)
		[Test]
		public void CombatBusExistsUnderSfx()
		{
			System.Collections.Generic.HashSet<string> buses = FmodProjectPaths.LoadBuses();
			Assert.IsTrue(buses.Contains("bus:/SFX"), string.Join(", ", buses));
			Assert.IsTrue(buses.Contains("bus:/SFX/Combat"), string.Join(", ", buses));
		}
	}
}
