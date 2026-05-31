using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// TASK-WM-169 Phase 1c — 보관 상자 내용물이 실제 세이브 경로(BuildingInstanceData.RuntimeData →
	// GridData → WorldStageSaveData)를 왕복해도 유지됨을 잠근다.
	// = "상자에 넣고 → 리로드 → 유지" 약속의 데이터층 behavior-verify (Play/DI/UI 불요).
	// WorldStage 는 Stage:DataSO(ScriptableObject) — CreateInstance + GridData=new() 즉시 사용 (WorldStageCitySaveTest 선례).
	public sealed class ChestPersistenceTest
	{
		private static WorldStage NewStage() => ScriptableObject.CreateInstance<WorldStage>();

		[Test]
		public void ChestContents_SurviveWorldStageSaveLoad()
		{
			Vector3Int pivot = new(0, 0, 0);

			ChestInventory chest = new();
			chest.Add(100, 3);
			chest.Add(205, 1);

			WorldStage original = NewStage();
			original.GridData.AddBuildingAt(pivot, new BuildingInstanceData(9000, runtimeData: chest.ToJson()));

			WorldStageSaveData saved = original.Save();

			WorldStage restored = NewStage();
			restored.Load(saved);

			BuildingInstanceData restoredData = restored.GridData.BuildingData[pivot];
			ChestInventory restoredChest = ChestInventory.FromJson(restoredData.RuntimeData);

			Assert.AreEqual(3, restoredChest.GetCount(100), "상자 아이템100 x3 세이브/로드 후 유지");
			Assert.AreEqual(1, restoredChest.GetCount(205), "상자 아이템205 x1 유지");
		}

		[Test]
		public void EmptyChest_RuntimeDataRoundTrips_NoError()
		{
			Vector3Int pivot = new(2, 2, 0);

			ChestInventory empty = new();

			WorldStage original = NewStage();
			original.GridData.AddBuildingAt(pivot, new BuildingInstanceData(9000, runtimeData: empty.ToJson()));

			WorldStage restored = NewStage();
			restored.Load(original.Save());

			BuildingInstanceData restoredData = restored.GridData.BuildingData[pivot];
			ChestInventory restoredChest = ChestInventory.FromJson(restoredData.RuntimeData);

			Assert.AreEqual(0, restoredChest.GetCount(100), "빈 상자 round-trip 후도 비어있음");
		}

		[Test]
		public void LegacyBuilding_NoRuntimeData_GivesEmptyChest()
		{
			// 옛 세이브/일반 건물 = RuntimeData 기본값("") → FromJson 빈 인벤토리 (NRE/throw 금지).
			Vector3Int pivot = new(4, 4, 0);

			WorldStage original = NewStage();
			original.GridData.AddBuildingAt(pivot, new BuildingInstanceData(9000));

			WorldStage restored = NewStage();
			restored.Load(original.Save());

			BuildingInstanceData restoredData = restored.GridData.BuildingData[pivot];
			Assert.DoesNotThrow(() => ChestInventory.FromJson(restoredData.RuntimeData));
			Assert.AreEqual(0, ChestInventory.FromJson(restoredData.RuntimeData).GetCount(100));
		}
	}
}
