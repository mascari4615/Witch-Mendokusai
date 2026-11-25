using System.Collections.Generic;
using System.Text.RegularExpressions;
using KarmoLabs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 밭의 정본은 <b>스테이지</b>다. 씬 오브젝트를 따라다니면 스테이지를 나갔다 오는
	/// 사이 심은 것이 사라지고, 스테이지 SO 는 재진입 사이 살아남으므로 안 비우면 지난 밭이 겹쳐 쌓인다.
	/// </summary>
	public sealed class WorldStageFarmTest
	{
		private const int PLANT_ID = 4615;
		private const float MAX_VITALITY = 100f;

		private static readonly FarmCoord SOIL = new(1, 63, 1);

		private static PlantGrowthParams CozyParams() => new(60, 2, MAX_VITALITY, 0f, 30f);

		private SOManager soManager;
		private readonly List<WitchPlantSO> plants = new();

		[SetUp]
		public void SetUp()
		{
			soManager = ScriptableObject.CreateInstance<SOManager>();
			SOManagerBridge.Register(soManager);
		}

		[TearDown]
		public void TearDown()
		{
			SOManagerBridge.Register(null);
			Object.DestroyImmediate(soManager);
			plants.ForEach(Object.DestroyImmediate);
			plants.Clear();
		}

		private void RegisterPlant(int plantId)
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults();
			plants.Add(plant);
			soManager.DataSOs[typeof(WitchPlantSO)] = new Dictionary<int, DataSO> { [plantId] = plant };
		}

		[Test]
		public void Stage_WritesItsFarm()
		{
			WorldStage stage = ScriptableObject.CreateInstance<WorldStage>();
			try
			{
				stage.Farm.AddPlot(SOIL).Plant(PLANT_ID, CozyParams(), MAX_VITALITY);

				WorldStageSaveData save = stage.Save();

				Assert.That(save.FarmSaveData, Is.Not.Null);
				Assert.That(save.FarmSaveData.Plots.Count, Is.EqualTo(1), "심긴 칸이 기억된다");
				Assert.That(save.FarmSaveData.Plots[0].PlantDataId, Is.EqualTo(PLANT_ID));
				Assert.That(save.FarmSaveData.Plots[0].Coord, Is.EqualTo(SOIL));
			}
			finally
			{
				Object.DestroyImmediate(stage);
			}
		}

		[Test]
		public void Load_Replaces_NotMerges()
		{
			// 스테이지 SO 는 재진입 사이 살아남는다 — 옛 밭이 남아 있으면 두 세이브가 겹쳐 자란다.
			WorldStage stage = ScriptableObject.CreateInstance<WorldStage>();
			try
			{
				stage.Farm.AddPlot(SOIL).Plant(PLANT_ID, CozyParams(), MAX_VITALITY);
				Assert.That(stage.Farm.PlotCount, Is.EqualTo(1));

				stage.Load(new WorldStageSaveData());

				Assert.That(stage.Farm.PlotCount, Is.EqualTo(0), "밭 없는 세이브를 열면 밭도 비워진다");
			}
			finally
			{
				Object.DestroyImmediate(stage);
			}
		}

		[Test]
		public void OldSave_WithoutAFarm_LoadsFine()
		{
			// 옛 세이브엔 밭 자체가 없다(null) = 그때는 밭이 없던 세계다. 터지면 안 된다.
			WorldStage stage = ScriptableObject.CreateInstance<WorldStage>();
			try
			{
				Assert.DoesNotThrow(() => stage.Load(new WorldStageSaveData { FarmSaveData = null }));
				Assert.That(stage.Farm, Is.Not.Null);
			}
			finally
			{
				Object.DestroyImmediate(stage);
			}
		}

		[Test]
		public void Load_RestoresWhatWasSaved()
		{
			WorldStage before = ScriptableObject.CreateInstance<WorldStage>();
			WorldStage after = ScriptableObject.CreateInstance<WorldStage>();
			try
			{
				RegisterPlant(PLANT_ID);
				GreenhousePlot planted = before.Farm.AddPlot(SOIL);
				planted.Plant(PLANT_ID, CozyParams(), MAX_VITALITY);
				planted.Step(70);
				planted.Observe();

				after.Load(SaveTool.CloneThroughJson(before.Save()));

				GreenhousePlot restored = after.Farm.GetPlot(SOIL);
				Assert.That(restored, Is.Not.Null, "저장된 밭이 불러올 때 사라진다");
				Assert.That(restored.PlantDataId, Is.EqualTo(PLANT_ID));
				Assert.That(restored.GrowthMinutes, Is.EqualTo(70));
				Assert.That(restored.Vitality, Is.EqualTo(planted.Vitality));
				Assert.That(restored.Observed, Is.True);
			}
			finally
			{
				Object.DestroyImmediate(before);
				Object.DestroyImmediate(after);
			}
		}

		[Test]
		public void Load_GrowsRealClockPlants_ForTimeAway()
		{
			const int AWAY_SECONDS = 30 * 60;
			WorldStage stage = ScriptableObject.CreateInstance<WorldStage>();
			try
			{
				RegisterPlant(PLANT_ID);
				stage.Farm.AddPlot(SOIL).Plant(PLANT_ID, CozyParams(), MAX_VITALITY, PlantClock.Real);
				WorldStageSaveData save = stage.Save();
				save.FarmSaveData.Plots[0].LastSeenStamp -= AWAY_SECONDS;

				stage.Load(save);

				// 저장과 불러오기 사이에 초가 넘어갈 수 있어 1분 여유
				int expectedMinutes = (int)(AWAY_SECONDS / stage.RealSecondsPerGrowthMinute);
				Assert.That(stage.Farm.GetPlot(SOIL).GrowthMinutes, Is.EqualTo(expectedMinutes).Within(1), "꺼 둔 동안 자란다");
			}
			finally
			{
				Object.DestroyImmediate(stage);
			}
		}

		[Test]
		public void Load_SkipsPlants_ThisBuildNoLongerHas()
		{
			FarmCoord otherSoil = new(2, 63, 1);
			WorldStage stage = ScriptableObject.CreateInstance<WorldStage>();
			try
			{
				RegisterPlant(PLANT_ID);
				stage.Farm.AddPlot(SOIL).Plant(PLANT_ID + 1, CozyParams(), MAX_VITALITY);
				stage.Farm.AddPlot(otherSoil).Plant(PLANT_ID, CozyParams(), MAX_VITALITY);
				WorldStageSaveData save = stage.Save();
				LogAssert.Expect(LogType.Warning, new Regex("밭 1칸"));

				stage.Load(save);

				Assert.That(stage.Farm.PlotCount, Is.EqualTo(1));
				Assert.That(stage.Farm.GetPlot(otherSoil), Is.Not.Null);
			}
			finally
			{
				Object.DestroyImmediate(stage);
			}
		}
	}
}
