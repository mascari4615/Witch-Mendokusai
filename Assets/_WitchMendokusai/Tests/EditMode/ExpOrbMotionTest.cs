using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class ExpOrbMotionTest
	{
		private SurvivalFeelConfig config;

		[SetUp]
		public void SetUp() => config = ScriptableObject.CreateInstance<SurvivalFeelConfig>();

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(config);

		[TestCase(0, 0)]
		[TestCase(14, 0)]
		[TestCase(15, 1)]
		[TestCase(59, 1)]
		[TestCase(60, 2)]
		[TestCase(5000, 2)]
		public void TierIndexFollowsMinValue(int value, int expected)
			=> Assert.AreEqual(expected, ExpOrbMotion.TierIndex(config.ExpGemTiers, value));

		[Test]
		public void PhaseSpreadsAcrossOrbs()
		{
			float first = ExpOrbMotion.PhaseOf(0);
			float second = ExpOrbMotion.PhaseOf(1);
			Assert.AreNotEqual(first, second);
			for (int i = 0; i < 300; i++)
				Assert.That(ExpOrbMotion.PhaseOf(i), Is.InRange(0f, 1f));
		}

		[Test]
		public void SizeStaysNearBaseAndScalesByTier()
		{
			float tier1 = ExpOrbMotion.Size(config, 1f, 0f, 0f);
			float tier3 = ExpOrbMotion.Size(config, 1.8f, 0f, 0f);
			Assert.AreEqual(1.8f, tier3 / tier1, 0.001f);
			float baseSize = config.ExpOrbSize * config.ExpOrbCubeRatio;
			for (float t = 0f; t < 5f; t += 0.1f)
				Assert.AreEqual(baseSize, ExpOrbMotion.Size(config, 1f, 0.3f, t), baseSize * config.ExpOrbPulseAmplitude + 0.0001f);
		}

		[Test]
		public void LiftNeverGoesBelowGround()
		{
			for (float t = 0f; t < 10f; t += 0.05f)
				Assert.That(ExpOrbMotion.Lift(config, 0.7f, t), Is.InRange(0f, config.ExpOrbBobAmplitude + 0.0001f));
		}

		[Test]
		public void ZeroAmplitudeFreezesPulseAndBob()
		{
			config.ExpOrbPulseAmplitude = 0f;
			config.ExpOrbBobAmplitude = 0f;
			Assert.AreEqual(ExpOrbMotion.Size(config, 1f, 0f, 0f), ExpOrbMotion.Size(config, 1f, 0f, 3.3f), 0.00001f);
			Assert.AreEqual(0f, ExpOrbMotion.Lift(config, 0.4f, 3.3f), 0.00001f);
		}

		[Test]
		public void SpinAdvancesWithTime()
		{
			Quaternion a = ExpOrbMotion.Rotation(config, 0f, 0f);
			Quaternion b = ExpOrbMotion.Rotation(config, 0f, 1f);
			Assert.AreNotEqual(0f, Quaternion.Angle(a, b));
		}
	}
}
