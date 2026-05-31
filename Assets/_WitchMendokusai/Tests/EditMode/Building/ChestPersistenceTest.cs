using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// TASK-WM-169 P1c — 상자 영속 seam: 상자 인벤토리 JSON 이 BuildingInstanceData.RuntimeData 에 실려
	// 실제 세이브 경로(GridData → WorldStageSaveData)를 왕복해도 보존됨을 잠근다 (model-agnostic 문자열 왕복).
	// 상자는 ChestStorageInventory.Save()(List<InventorySlotSaveData>)를 JSON 으로 RuntimeData 에 싣는다.
	// Item 재구성(SOHelper.GetItemData)은 런타임 레지스트리 전용 → Play 검증 영역, 여기선 seam(문자열 보존)만 잠금.
	// WorldStage = Stage:DataSO(ScriptableObject), Save/Load = POCO 왕복 (WorldStageCitySaveTest 선례).
	public sealed class ChestPersistenceTest
	{
		private static WorldStage NewStage() => ScriptableObject.CreateInstance<WorldStage>();

		[Test]
		public void ChestRuntimeData_SurvivesWorldStageSaveLoad()
		{
			Vector3Int pivot = new(0, 0, 0);
			// 상자 인벤토리 직렬화 결과 모사 (실제 = ChestStorageInventory.Save() JSON).
			string chestJson = "[{\"slotIndex\":0,\"itemID\":10000000,\"itemAmount\":3}]";

			WorldStage original = NewStage();
			original.GridData.AddBuildingAt(pivot, new BuildingInstanceData(9000, runtimeData: chestJson));

			WorldStage restored = NewStage();
			restored.Load(original.Save());

			BuildingInstanceData restoredData = restored.GridData.BuildingData[pivot];
			Assert.AreEqual(chestJson, restoredData.RuntimeData, "상자 RuntimeData(인벤토리 JSON) 세이브/로드 후 보존");
		}

		[Test]
		public void Building_EmptyRuntimeData_SafeRoundTrip()
		{
			Vector3Int pivot = new(4, 4, 0);

			WorldStage original = NewStage();
			original.GridData.AddBuildingAt(pivot, new BuildingInstanceData(9000));

			WorldStage restored = NewStage();
			restored.Load(original.Save());

			BuildingInstanceData restoredData = restored.GridData.BuildingData[pivot];
			Assert.That(string.IsNullOrEmpty(restoredData.RuntimeData), Is.True, "RuntimeData 없는 건물도 안전 왕복");
		}
	}
}
