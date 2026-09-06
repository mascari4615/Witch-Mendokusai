using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 옛 밭(실시간 초)이 지금 규칙의 「가장 단순한 경우」임을 못박는다:
	/// 단계 1개 · 절대 안 시듦. 그래야 두 밭이 한 규칙 위에 선다.
	/// </summary>
	public sealed class LegacyGrowthTest
	{
		private const float REAL_SECONDS_PER_MINUTE = 60f;

		[Test]
		public void OldSeconds_BecomeMinutes_RoundedUp()
		{
			// 내림하면 30초짜리가 0분 = 심자마자 수확이 되어 옛 밭보다 더 이상해진다.
			Assert.That(LegacyGrowth.FromSeconds(30f, REAL_SECONDS_PER_MINUTE).MinutesPerStage, Is.EqualTo(1));
			Assert.That(LegacyGrowth.FromSeconds(60f, REAL_SECONDS_PER_MINUTE).MinutesPerStage, Is.EqualTo(1));
			Assert.That(LegacyGrowth.FromSeconds(61f, REAL_SECONDS_PER_MINUTE).MinutesPerStage, Is.EqualTo(2));
			Assert.That(LegacyGrowth.FromSeconds(0f, REAL_SECONDS_PER_MINUTE).MinutesPerStage, Is.EqualTo(1), "0초도 최소 1분");
		}

		[Test]
		public void OldCrops_NeverWither_AndNeedNoCare()
		{
			PlantGrowthParams parameters = LegacyGrowth.FromSeconds(120f, REAL_SECONDS_PER_MINUTE);

			Assert.That(parameters.DrainPerMinute, Is.EqualTo(0f), "옛 밭엔 시듦이 없었다");
			Assert.That(parameters.MaxStage, Is.EqualTo(1), "옛 밭엔 단계가 없었다");
		}

		[Test]
		public void OldCrop_RunsOnTheCurrentRules()
		{
			// 다리를 건넌 옛 작물이 지금 규칙 그대로 자라고 거둬진다.
			Greenhouse greenhouse = new();
			FarmCoord soil = new(0, 63, 0);
			PlantGrowthParams parameters = LegacyGrowth.FromSeconds(120f, REAL_SECONDS_PER_MINUTE);
			greenhouse.AddPlot(soil).Plant(1, parameters, 100f, PlantClock.Real);

			greenhouse.TickWithCarers(null, 1, PlantClock.Real);
			Assert.That(greenhouse.GetPlot(soil).Phase, Is.EqualTo(PlotPhase.Growing));

			greenhouse.TickWithCarers(null, 1, PlantClock.Real);
			Assert.That(greenhouse.GetPlot(soil).Phase, Is.EqualTo(PlotPhase.Bloomed), "2분이면 다 자란다");
			Assert.That(greenhouse.GetPlot(soil).TryHarvest(out _), Is.True);
		}

		[Test]
		public void WorldScaledSeconds_FollowTheWorldsRate()
		{
			// 세계가 「현실 10초 = 성장 1분」이면 옛 밭도 그 환산을 탄다(같은 수를 두 곳에 안 적는다).
			Assert.That(LegacyGrowth.FromSeconds(30f, 10f).MinutesPerStage, Is.EqualTo(3));
		}
	}
}
