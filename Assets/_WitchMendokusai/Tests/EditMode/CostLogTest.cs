using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public class CostLogTest
	{
		[TestCase(10.0, 10f, false)]
		[TestCase(10.01, 10f, true)]
		[TestCase(3.0, 10f, false)]
		public void LogOnlyAboveThreshold(double milliseconds, float thresholdMilliseconds, bool expected)
			=> Assert.AreEqual(expected, CostLog.ShouldLog(milliseconds, thresholdMilliseconds));

		// 환경 변수 WM_COST_MS. 이상한 값은 기본 문턱 (NaN 이면 모든 구간을 찍게 됨)
		[TestCase(null, 10f)]
		[TestCase("", 10f)]
		[TestCase("25", 25f)]
		[TestCase("0.5", 0.5f)]
		[TestCase("NaN", 10f)]
		[TestCase("-1", 10f)]
		[TestCase("abc", 10f)]
		public void ThresholdFromEnvironmentText(string text, float expected)
			=> Assert.AreEqual(expected, CostLog.ParseThreshold(text, 10f));

		// 분석은 이 모양을 읽음 (memo wm/reference/demo-audit-20261123/README.md 의 구간 비용 기록 절). 칸을 바꾸면 같이
		[Test]
		public void LineCarriesEveryField()
		{
			string line = CostLog.Format("EndDungeon.Save", 34.26, 811.14f, 48211);
			Assert.AreEqual("[Cost] EndDungeon.Save 34.3ms t=811.1s frame=48211", line);
		}

		[Test]
		public void ElapsedUsesStopwatchFrequency()
			=> Assert.AreEqual(250.0, CostLog.ElapsedMilliseconds(0, Stopwatch.Frequency / 4), 0.001);

		// 소수점이 쉼표인 기계에서도 분석이 읽을 수 있게
		[Test]
		public void FormatAndParseIgnoreMachineCulture()
		{
			CultureInfo previous = Thread.CurrentThread.CurrentCulture;
			try
			{
				Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
				StringAssert.Contains("34.3ms", CostLog.Format("X", 34.26, 1f, 1));
				Assert.AreEqual(0.5f, CostLog.ParseThreshold("0.5", 10f));
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture = previous;
			}
		}

		[Test]
		public void NullLabelFailsFast()
			=> Assert.Throws<ArgumentNullException>(() => CostLog.Measure(null));
	}
}
