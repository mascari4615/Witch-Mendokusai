using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class ExpPickupPitchTest
	{
		private static readonly List<int> PENTATONIC = new() { 0, 2, 4, 7, 9 };

		[TestCase(0f, 0f)]
		[TestCase(1.9f, 0f)]
		[TestCase(2f, 2f)]
		[TestCase(6.5f, 4f)]
		[TestCase(7f, 7f)]
		[TestCase(11.9f, 9f)]
		[TestCase(12f, 12f)]
		[TestCase(14.2f, 14f)]
		[TestCase(-0.5f, -3f)]
		public void SnapDownPicksScaleNoteAtOrBelow(float semitones, float expected)
			=> Assert.AreEqual(expected, ExpPickupPitch.SnapDown(semitones, PENTATONIC), 0.0001f);

		[Test]
		public void PitchRisesWithProgressAndStartsAtBase()
		{
			AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 7f);
			float start = ExpPickupPitch.Multiplier(0f, curve, PENTATONIC);
			float middle = ExpPickupPitch.Multiplier(0.5f, curve, PENTATONIC);
			float full = ExpPickupPitch.Multiplier(1f, curve, PENTATONIC);
			Assert.AreEqual(1f, start, 0.0001f);
			Assert.Less(start, middle);
			Assert.Less(middle, full);
			Assert.AreEqual(Mathf.Pow(2f, 7f / 12f), full, 0.0001f);
		}

		[Test]
		public void EmptyScaleKeepsCurveSmooth()
		{
			AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 7f);
			Assert.AreEqual(Mathf.Pow(2f, 3.5f / 12f), ExpPickupPitch.Multiplier(0.5f, curve, new List<int>()), 0.0001f);
		}

		// 진행 1 은 그 자리에서 레벨업하므로 기본 곡선은 1 직전에 음계 최고음 (9) 까지 닿아야 함
		[Test]
		public void DefaultConfigReachesTopScaleNoteBeforeLevelUp()
		{
			SurvivalFeelConfig config = ScriptableObject.CreateInstance<SurvivalFeelConfig>();
			try
			{
				float nearFull = ExpPickupPitch.Multiplier(0.95f, config.ExpPitchSemitones, config.ExpPitchScale);
				Assert.AreEqual(Mathf.Pow(2f, 9f / 12f), nearFull, 0.0001f);
			}
			finally
			{
				Object.DestroyImmediate(config);
			}
		}

		[TestCase(0, 0f)]
		[TestCase(10, -3f)]
		[TestCase(20, -6f)]
		[TestCase(100, -6f)]
		[TestCase(-5, 0f)]
		public void LevelVolumeStepsDownToFloor(int level, float expectedDb)
			=> Assert.AreEqual(Mathf.Pow(10f, expectedDb / 20f), ExpPickupPitch.LevelVolume(level, -0.3f, -6f), 0.0001f);

		[Test]
		public void LevelVolumeNeverBoostsAboveUnity()
			=> Assert.AreEqual(1f, ExpPickupPitch.LevelVolume(10, 0.5f, -6f), 0.0001f);

		[TestCase(0, 10, 0f)]
		[TestCase(5, 10, 0.5f)]
		[TestCase(15, 10, 1f)]
		[TestCase(3, 0, 0f)]
		public void ProgressIsClampedRatio(int currentExp, int requiredExp, float expected)
			=> Assert.AreEqual(expected, ExpPickupPitch.Progress(currentExp, requiredExp), 0.0001f);

		[TestCase(0, 6, 1f, 0f, 0.03f, true)]
		[TestCase(6, 6, 1f, 0f, 0.03f, false)]
		[TestCase(2, 6, 1.01f, 1f, 0.03f, false)]
		[TestCase(2, 6, 1.05f, 1f, 0.03f, true)]
		public void VoiceGateRespectsCountAndInterval(int playing, int maxVoices, float now, float lastStart, float minInterval, bool expected)
			=> Assert.AreEqual(expected, SfxVoiceGate.CanStart(playing, maxVoices, now, lastStart, minInterval));
	}
}
