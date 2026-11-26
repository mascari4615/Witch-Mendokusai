using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public class HitchLogTest
	{
		[TestCase(100f, 100f, false)]
		[TestCase(100.5f, 100f, true)]
		[TestCase(16f, 100f, false)]
		public void HitchOnlyAboveThreshold(float frameMilliseconds, float thresholdMilliseconds, bool expected)
			=> Assert.AreEqual(expected, HitchLog.IsHitch(frameMilliseconds, thresholdMilliseconds));

		// 사람이 하는 판만 잼. 무인 신호 하나라도 있으면 면제 (FocusPause 와 같은 신호)
		[TestCase(false, false, false, false, false)]
		[TestCase(true, false, false, false, true)]
		[TestCase(false, true, false, false, true)]
		[TestCase(false, false, true, false, true)]
		[TestCase(false, false, false, true, true)]
		public void ExemptWhenAnyUnattendedSignal(bool isEditor, bool isBatchMode, bool isDeterministic, bool isSilentRun, bool expected)
			=> Assert.AreEqual(expected, HitchLog.IsExempt(isEditor, isBatchMode, isDeterministic, isSilentRun));

		// 분석은 이 모양을 정규식으로 읽음 (memo wm/reference/demo-audit-20261123/release-run-20261123/hitches.mjs). 칸을 바꾸면 같이
		[Test]
		public void LineCarriesEveryField()
		{
			string line = HitchLog.Format(3, 517.4f, 39.62f, "Dungeon", 0f, 1f, 1, 2048, 0);
			Assert.AreEqual("[Hitch] #3 517ms t=39.6s scene=Dungeon timeScale=0.00>1.00 gc=+1 heap=+2KB loads=+0", line);
		}

		[Test]
		public void NegativeHeapKeepsSign()
			=> StringAssert.Contains("heap=-3KB", HitchLog.Format(1, 120f, 1f, "S", 1f, 1f, 1, -3072, 0));

		// 소수점이 쉼표인 기계에서도 분석 정규식이 읽을 수 있게
		[Test]
		public void FormatIgnoresMachineCulture()
		{
			CultureInfo previous = Thread.CurrentThread.CurrentCulture;
			try
			{
				Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
				StringAssert.Contains("t=39.6s", HitchLog.Format(1, 120f, 39.62f, "S", 1f, 1f, 0, 0, 0));
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture = previous;
			}
		}
	}
}
