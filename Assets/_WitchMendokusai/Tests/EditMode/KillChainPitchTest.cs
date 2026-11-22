using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public class KillChainPitchTest
	{
		private static readonly List<int> PENTATONIC = new() { 0, 2, 4, 7, 9 };

		[TestCase(0, 10f, 10.3f, 1)]
		[TestCase(3, 10f, 10.3f, 4)]
		[TestCase(4, 10f, 10.3f, 0)]
		[TestCase(3, 10f, 11f, 0)]
		public void StepClimbsInsideWindowAndWrapsAfterTop(int previousStep, float lastKillTime, float now, int expected)
			=> Assert.AreEqual(expected, KillChainPitch.NextStep(previousStep, lastKillTime, now, 0.5f, 4));

		[Test]
		public void FirstKillStartsAtBottom()
			=> Assert.AreEqual(0, KillChainPitch.NextStep(0, float.NegativeInfinity, 0f, 0.5f, 4));

		[TestCase(10.01f)]
		[TestCase(10f)]
		public void ZeroWindowTurnsChainOff(float now)
			=> Assert.AreEqual(0, KillChainPitch.NextStep(2, 10f, now, 0f, 4));

		[Test]
		public void WindowEdgeStillChains()
			=> Assert.AreEqual(3, KillChainPitch.NextStep(2, 10f, 10.5f, 0.5f, 4));

		[TestCase(0, 0)]
		[TestCase(3, 7)]
		[TestCase(5, 12)]
		[TestCase(6, 14)]
		public void StepMapsToScaleWithOctaveWrap(int step, int expected)
			=> Assert.AreEqual(expected, KillChainPitch.Semitones(step, PENTATONIC));

		[Test]
		public void MultiplierIsSemitoneRatio()
			=> Assert.AreEqual(Mathf.Pow(2f, 7f / 12f), KillChainPitch.Multiplier(3, PENTATONIC), 0.0001f);
	}
}
