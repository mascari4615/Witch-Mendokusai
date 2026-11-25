using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShurikenStormRulesTests
	{
		private const float STORM = 2.5f;
		private const float FAR = 8f;
		private const float OUT = 0.4f;
		private const float RETURN = 18f;

		[Test]
		public void CountAddsCardsAndStopsAtCap()
		{
			Assert.AreEqual(4, ShurikenStormRules.Count(4, 0, 0, 12));
			Assert.AreEqual(6, ShurikenStormRules.Count(4, 2, 0, 12));
			Assert.AreEqual(12, ShurikenStormRules.Count(4, 2, 6, 12));
			Assert.AreEqual(12, ShurikenStormRules.Count(4, 2, 20, 12));
			Assert.AreEqual(4, ShurikenStormRules.Count(4, -3, -1, 12), "음수 카드는 무시");
		}

		[Test]
		public void CloneStormIsCapped()
		{
			Assert.AreEqual(4, ShurikenStormRules.CloneCount(12, 4));
			Assert.AreEqual(3, ShurikenStormRules.CloneCount(3, 4));
		}

		[Test]
		public void AnglesAreEvenAndWrapped()
		{
			Assert.AreEqual(0f, ShurikenStormRules.Angle(0, 4, 0f), 0.001f);
			Assert.AreEqual(90f, ShurikenStormRules.Angle(1, 4, 0f), 0.001f);
			Assert.AreEqual(100f, ShurikenStormRules.Angle(3, 4, 190f), 0.001f);
			Assert.AreEqual(350f, ShurikenStormRules.Angle(0, 4, -10f), 0.001f);
			Assert.AreEqual(0f, ShurikenStormRules.Angle(0, 0, 50f), 0.001f);
			Assert.Less(ShurikenStormRules.Angle(0, 4, -0.000001f), 360f);
		}

		[Test]
		public void ScatterSlowsOutThenReturnsAtSteadySpeed()
		{
			float seconds = ShurikenStormRules.ScatterSeconds(STORM, FAR, OUT, RETURN);
			Assert.AreEqual(OUT + 5.5f / RETURN, seconds, 0.0001f);
			Assert.AreEqual(STORM, ShurikenStormRules.ScatterRadius(0f, STORM, FAR, OUT, RETURN), 0.0001f);
			Assert.AreEqual(FAR, ShurikenStormRules.ScatterRadius(OUT, STORM, FAR, OUT, RETURN), 0.0001f);
			Assert.AreEqual(STORM, ShurikenStormRules.ScatterRadius(seconds, STORM, FAR, OUT, RETURN), 0.0001f);
			float half = ShurikenStormRules.ScatterRadius(OUT / 2f, STORM, FAR, OUT, RETURN);
			Assert.Greater(half - STORM, FAR - half, "앞 절반에 더 멀리 감");
			Assert.AreEqual(FAR - 0.1f * RETURN, ShurikenStormRules.ScatterRadius(OUT + 0.1f, STORM, FAR, OUT, RETURN), 0.001f);
			Assert.AreEqual(STORM + 0.01f * RETURN, ShurikenStormRules.ScatterRadius(seconds - 0.01f, STORM, FAR, OUT, RETURN), 0.01f, "끝에서 순간이동하지 않음");
		}

		[Test]
		public void ScatterEdgeValues()
		{
			Assert.AreEqual(0f, ShurikenStormRules.ScatterSeconds(3.5f, 3f, OUT, RETURN), 0.0001f);
			Assert.AreEqual(3.5f, ShurikenStormRules.ScatterRadius(0.2f, 3.5f, 3f, OUT, RETURN), 0.0001f);
			Assert.AreEqual(ShurikenStormRules.ScatterRadius(0.001f, STORM, FAR, 0f, RETURN), ShurikenStormRules.ScatterRadius(0.001f, STORM, FAR, -0.2f, RETURN), 0.0001f);
		}

		// 한 자리의 적을 8초 동안 매 프레임 판정 (RepeatingDamagingObject 와 같은 꼴). 표창마다 따로 세면 수에 비례하고, 같이 세면 12자루도 4자루와 비슷
		[Test]
		public void SeparateRehitScalesWithCountAndSharedDoesNot()
		{
			float four = HitsPerSecond(4, 180f, 0.4f, false);
			Assert.AreEqual(2f, four, 0.15f);
			Assert.AreEqual(1.5f, HitsPerSecond(6, 180f, 0.4f, false) / four, 0.1f);
			Assert.AreEqual(3f, HitsPerSecond(12, 180f, 0.4f, false) / four, 0.15f);
			Assert.Less(HitsPerSecond(12, 180f, 0.4f, true) / HitsPerSecond(4, 180f, 0.4f, true), 1.5f);
		}

		[Test]
		public void FastSpinGainsItsSpeed()
		{
			Assert.AreEqual(1.5f, HitsPerSecond(4, 270f, 0.27f, false) / HitsPerSecond(4, 180f, 0.4f, false), 0.1f);
		}

		private static float HitsPerSecond(int count, float degreesPerSecond, float rehitSeconds, bool shared)
		{
			const float SECONDS = 8f;
			const int FRAMES_PER_SECOND = 60;
			const float HALF_ARC_DEGREES = 20f;
			const long FOE = 1;
			HitRule rule = new(false, false, rehitSeconds);
			HitLedger[] ledgers = new HitLedger[count];
			HitLedger sharedLedger = new();
			for (int i = 0; i < count; i++)
				ledgers[i] = shared ? sharedLedger : new HitLedger();
			int hits = 0;
			for (int frame = 0; frame < SECONDS * FRAMES_PER_SECOND; frame++)
			{
				float time = frame / (float)FRAMES_PER_SECOND;
				for (int i = 0; i < count; i++)
				{
					float angle = ShurikenStormRules.Angle(i, count, time * degreesPerSecond);
					bool overlaps = angle <= HALF_ARC_DEGREES || angle >= 360f - HALF_ARC_DEGREES;
					if (overlaps && ledgers[i].TryHit(FOE, time, frame, rule))
						hits++;
				}
			}
			return hits / SECONDS;
		}
	}
}
