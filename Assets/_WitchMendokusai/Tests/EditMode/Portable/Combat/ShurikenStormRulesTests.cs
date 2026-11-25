using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShurikenStormRulesTests
	{
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
		}

		[Test]
		public void ScatterGoesOutAndComesBack()
		{
			const float STORM = 2.5f;
			const float FAR = 8f;
			const float OUT = 0.4f;
			const float RETURN = 18f;
			float seconds = ShurikenStormRules.ScatterSeconds(STORM, FAR, OUT, RETURN);
			Assert.AreEqual(OUT + 5.5f / 18f, seconds, 0.0001f);
			Assert.AreEqual(STORM, ShurikenStormRules.ScatterRadius(0f, STORM, FAR, OUT, RETURN), 0.0001f);
			Assert.AreEqual(FAR, ShurikenStormRules.ScatterRadius(OUT, STORM, FAR, OUT, RETURN), 0.0001f);
			Assert.AreEqual(STORM, ShurikenStormRules.ScatterRadius(seconds, STORM, FAR, OUT, RETURN), 0.0001f);
			float previous = STORM;
			for (float time = 0.02f; time < OUT; time += 0.02f)
			{
				float radius = ShurikenStormRules.ScatterRadius(time, STORM, FAR, OUT, RETURN);
				Assert.Greater(radius, previous, $"{time} 초");
				previous = radius;
			}
		}

		[Test]
		public void ScatterInsideStormDoesNothing()
		{
			Assert.AreEqual(0f, ShurikenStormRules.ScatterSeconds(3.5f, 3f, 0.4f, 18f), 0.0001f);
			Assert.AreEqual(3.5f, ShurikenStormRules.ScatterRadius(0.2f, 3.5f, 3f, 0.4f, 18f), 0.0001f);
		}

		// 표창마다 재타격을 세면 12자루에서 같은 적을 한 바퀴 (2초) 에 12번. 하나를 나눠 쓰면 간격 0.4초로 많아야 다섯 번 (실제로는 지나가는 때에 맞춰 네 번)
		[Test]
		public void SharedLedgerKeepsHitRateBelowInterval()
		{
			Assert.AreEqual(2, CountHits(2, true));
			Assert.AreEqual(4, CountHits(4, true));
			Assert.LessOrEqual(CountHits(12, true), 5);
			Assert.AreEqual(12, CountHits(12, false));
		}

		// 초당 180도, 한 바퀴 2초 동안 한 자리의 적을 표창들이 차례로 지나감
		private static int CountHits(int shurikens, bool shared)
		{
			const float LAP_SECONDS = 2f;
			const long FOE = 1;
			HitRule rule = new(false, false, 0.4f);
			HitLedger sharedLedger = new();
			int hits = 0;
			for (int i = 0; i < shurikens; i++)
			{
				HitLedger ledger = shared ? sharedLedger : new HitLedger();
				if (ledger.TryHit(FOE, LAP_SECONDS * i / shurikens, i, rule))
					hits++;
			}
			return hits;
		}
	}
}
