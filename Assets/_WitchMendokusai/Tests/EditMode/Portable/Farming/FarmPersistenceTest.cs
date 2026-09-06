using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 밭을 적고 되살린다. 핵심은 <b>못 본 사이를 메우는 것</b>:
	/// 게임을 끄면 세계의 하늘은 멈추지만 바깥 현실은 안 멈춘다.
	/// </summary>
	public sealed class FarmPersistenceTest
	{
		private const int PLANT_ID = 4615;
		private const int MINUTES_PER_STAGE = 60;
		private const int MAX_STAGE = 2;
		private const float MAX_VITALITY = 100f;
		private const float REAL_SECONDS_PER_MINUTE = 60f;
		private const long UNIX_NOON = 1_800_000_000L;

		private static readonly FarmCoord SOIL = new(2, 63, 5);

		private static PlantGrowthParams CozyParams() => new(MINUTES_PER_STAGE, MAX_STAGE, MAX_VITALITY, 0f, 30f);

		private static PlantGrowthParams? Lookup(int plantDataId) => plantDataId == PLANT_ID ? CozyParams() : (PlantGrowthParams?)null;

		private static Greenhouse Planted(PlantClock clock)
		{
			Greenhouse greenhouse = new();
			greenhouse.AddPlot(SOIL).Plant(PLANT_ID, CozyParams(), MAX_VITALITY, clock);
			return greenhouse;
		}

		[Test]
		public void SaveThenLoad_KeepsWhatItWas()
		{
			Greenhouse before = Planted(PlantClock.World);
			before.GetPlot(SOIL).Observe();
			before.TickWithCarers(null, 30);

			FarmSaveData save = FarmPersistence.Save(before, worldMinutesNow: 500, realUnixSecondsNow: UNIX_NOON);

			Greenhouse after = new();
			int skipped = FarmPersistence.Load(after, save, 500, UNIX_NOON, REAL_SECONDS_PER_MINUTE, Lookup);

			Assert.That(skipped, Is.EqualTo(0));
			Assert.That(after.GetPlot(SOIL), Is.Not.Null, "같은 자리에 되살아난다");
			Assert.That(after.GetPlot(SOIL).PlantDataId, Is.EqualTo(PLANT_ID));
			Assert.That(after.GetPlot(SOIL).Observed, Is.True, "봐준 사실도 기억한다");
			Assert.That(after.GetPlot(SOIL).GrowthMinutes, Is.EqualTo(30), "자란 만큼 그대로");
			Assert.That(after.GetPlot(SOIL).Clock, Is.EqualTo(PlantClock.World));
		}

		[Test]
		public void RealTimeCrop_GrowsWhileTheGameWasOff()
		{
			Greenhouse before = Planted(PlantClock.Real);
			FarmSaveData save = FarmPersistence.Save(before, 0, UNIX_NOON);

			// 두 시간을 꺼 뒀다 — 현실 60초 = 성장 1분이니 120분어치.
			Greenhouse after = new();
			FarmPersistence.Load(after, save, 0, UNIX_NOON + 2 * 60 * 60, REAL_SECONDS_PER_MINUTE, Lookup);

			Assert.That(after.GetPlot(SOIL).GrowthMinutes, Is.EqualTo(120), "꺼 둔 동안도 자란다");
			Assert.That(after.GetPlot(SOIL).Phase, Is.EqualTo(PlotPhase.Bloomed));
		}

		[Test]
		public void WorldTimeCrop_DoesNotGrowWhileTheGameWasOff()
		{
			Greenhouse before = Planted(PlantClock.World);
			FarmSaveData save = FarmPersistence.Save(before, worldMinutesNow: 1000, realUnixSecondsNow: UNIX_NOON);

			// 현실로 이틀이 지났어도 세계의 하늘은 그대로 1000분이다.
			Greenhouse after = new();
			FarmPersistence.Load(after, save, 1000, UNIX_NOON + 2 * 24 * 60 * 60, REAL_SECONDS_PER_MINUTE, Lookup);

			Assert.That(after.GetPlot(SOIL).GrowthMinutes, Is.EqualTo(0), "하늘이 안 흘렀으면 안 자란다");
		}

		[Test]
		public void WorldTimeCrop_CatchesUpWithTheSky()
		{
			Greenhouse before = Planted(PlantClock.World);
			FarmSaveData save = FarmPersistence.Save(before, worldMinutesNow: 1000, realUnixSecondsNow: UNIX_NOON);

			Greenhouse after = new();
			FarmPersistence.Load(after, save, 1000 + 90, UNIX_NOON, REAL_SECONDS_PER_MINUTE, Lookup);

			Assert.That(after.GetPlot(SOIL).GrowthMinutes, Is.EqualTo(90), "하늘이 흐른 만큼만");
		}

		[Test]
		public void TimeNeverRunsBackwards()
		{
			// 기기 시각이 뒤로 가거나 저장이 미래를 가리켜도 자란 것이 도로 어려지지 않는다.
			Greenhouse before = Planted(PlantClock.Real);
			FarmSaveData save = FarmPersistence.Save(before, 0, UNIX_NOON);

			Greenhouse after = new();
			FarmPersistence.Load(after, save, 0, UNIX_NOON - 10_000, REAL_SECONDS_PER_MINUTE, Lookup);

			Assert.That(after.GetPlot(SOIL).GrowthMinutes, Is.EqualTo(0));
		}

		[Test]
		public void UnknownPlant_IsReportedNotSwallowed()
		{
			Greenhouse before = Planted(PlantClock.World);
			FarmSaveData save = FarmPersistence.Save(before, 0, UNIX_NOON);
			save.Plots[0].PlantDataId = 999999; // 카탈로그에서 사라진 작물(모드 제거 등)

			Greenhouse after = new();
			int skipped = FarmPersistence.Load(after, save, 0, UNIX_NOON, REAL_SECONDS_PER_MINUTE, Lookup);

			Assert.That(skipped, Is.EqualTo(1), "모르는 작물은 조용히 없어지지 않고 세어 돌려준다");
			Assert.That(after.PlotCount, Is.EqualTo(0));
		}

		[Test]
		public void EmptyPlots_AreNotWritten()
		{
			Greenhouse greenhouse = new();
			greenhouse.AddPlot(SOIL); // 갈아만 두고 안 심은 칸

			FarmSaveData save = FarmPersistence.Save(greenhouse, 0, UNIX_NOON);

			Assert.That(save.Plots.Count, Is.EqualTo(0), "빈 칸은 적을 것이 없다");
		}
	}
}
