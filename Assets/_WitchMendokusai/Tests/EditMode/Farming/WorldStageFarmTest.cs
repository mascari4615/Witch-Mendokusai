using NUnit.Framework;
using UnityEngine;
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
	}
}
