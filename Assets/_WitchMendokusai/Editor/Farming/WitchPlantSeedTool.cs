using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 마도 온실 「봐줘야 진짜가 된다」 + 수확 루프가 인게임에 보이려면 *등록된* 데이터가 필요하다:
	//   ① WitchPlantSO 종 ≥1 (Discovery 도감 — DataLoader Addressable 로드 → SOManager.DataSOs → PlantDiscoveryCategory)
	//   ② 그 종의 HarvestLoots 에 ItemData ≥1 (수확 → 인벤토리 → 마도서 ItemCountCriteria 자동 집계)
	// 둘 다 0이면 "수확했는데 아무것도 없다"(도감/인벤토리 무음). 이 도구가 그 최소 데이터를 멱등 보장 + canon
	// Addressable 등록(DataSOUtil.SetAddressableAsset). 종/아이템 식별(이름·스프라이트·수치·변이표)=사용자 Grey Box —
	// 샘플은 placeholder, WM/DataSOWindow 개명·재튜닝. process.md § 수동 트리거 전제 = MenuItem 동반.
	public static class WitchPlantSeedTool
	{
		private const string PARENT_FOLDER = "Assets/_WitchMendokusai/Domain/Farming";
		private const string PLANTS_SUBFOLDER = "Plants";
		private const string PLANTS_FOLDER = PARENT_FOLDER + "/" + PLANTS_SUBFOLDER;

		// placeholder 디제틱 이름 — 마도 작물(시드는 종). 사용자가 DataSOWindow 로 개명.
		private const string SAMPLE_PLANT_NAME = "달빛이끼";
		private const int SAMPLE_PLANT_ID = 0;

		// 수확물 ItemData — 예약 고ID(기존 ItemData ID 충돌 회피). 마도서 챕터가 이 ID 를 재료로 선언하면 자동 집계.
		private const string SAMPLE_ITEM_NAME = "달빛이끼 잎";
		private const int SAMPLE_ITEM_ID = 90_000_167;

		[MenuItem("WM/Farming/Ensure Sample Plant")]
		public static WitchPlantSO EnsureSamplePlant()
		{
			ItemData harvestItem = EnsureSampleHarvestItem();
			WitchPlantSO plant = FindOrCreatePlant();
			EnsureHarvestLootWired(plant, harvestItem);
			return plant;
		}

		// 종 SO 멱등 — 있으면 첫 종 Addressable 보강, 없으면 샘플 생성.
		private static WitchPlantSO FindOrCreatePlant()
		{
			string[] existingGuids = AssetDatabase.FindAssets($"t:{nameof(WitchPlantSO)}");
			if (existingGuids.Length > 0)
			{
				WitchPlantSO found = AssetDatabase.LoadAssetAtPath<WitchPlantSO>(AssetDatabase.GUIDToAssetPath(existingGuids[0]));
				DataSOUtil.SetAddressableAsset(found);
				return found;
			}

			EnsureFolder();

			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults(); // CreateInstance 는 [field: SerializeField] 이니셜라이저를 0으로 덮음 → 재주입.
			plant.ID = SAMPLE_PLANT_ID;
			plant.Name = SAMPLE_PLANT_NAME;

			CreateAndRegister(plant, DataSODefine.AssetPrefixes[typeof(WitchPlantSO)]);
			Debug.Log($"[WitchPlantSeed] 샘플 종 생성: '{plant.Name}'(ID {plant.ID}) + Addressable 등록");
			return plant;
		}

		// 수확물 ItemData 멱등 — 예약 ID 로 조회, 없으면 생성.
		public static ItemData EnsureSampleHarvestItem()
		{
			foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(ItemData)}"))
			{
				ItemData existing = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
				if (existing != null && existing.ID == SAMPLE_ITEM_ID)
				{
					DataSOUtil.SetAddressableAsset(existing);
					return existing;
				}
			}

			EnsureFolder();

			ItemData item = ScriptableObject.CreateInstance<ItemData>();
			item.ID = SAMPLE_ITEM_ID;
			item.Name = SAMPLE_ITEM_NAME;

			CreateAndRegister(item, DataSODefine.AssetPrefixes[typeof(ItemData)]);
			Debug.Log($"[WitchPlantSeed] 샘플 수확물 생성: '{item.Name}'(ID {item.ID}) + Addressable 등록");
			return item;
		}

		// 종 HarvestLoots 비었으면 샘플 수확물 100% 로 배선 → 수확 시 인벤토리에 들어감(루프 demonstrable).
		private static void EnsureHarvestLootWired(WitchPlantSO plant, ItemData harvestItem)
		{
			if (plant == null || harvestItem == null)
			{
				return;
			}

			if (plant.HarvestLoots != null && plant.HarvestLoots.Count > 0)
			{
				return; // 이미 디자이너가 채움 — 덮지 않음.
			}

			plant.EditorSetHarvestLoots(new List<DataSOWithPercentage>
			{
				new DataSOWithPercentage { DataSO = harvestItem, Percentage = 100f },
			});
			EditorUtility.SetDirty(plant);
			AssetDatabase.SaveAssets();
			Debug.Log($"[WitchPlantSeed] '{plant.Name}' HarvestLoots ← '{harvestItem.Name}' 100% 배선 (수확→인벤토리→마도서 ready)");
		}

		private static void CreateAndRegister(DataSO dataSO, string prefix)
		{
			string assetName = DataSOUtil.ConvertToCorrectAssetName($"{prefix}_{dataSO.ID}_{dataSO.Name}");
			string path = AssetDatabase.GenerateUniqueAssetPath($"{PLANTS_FOLDER}/{assetName}.asset");
			AssetDatabase.CreateAsset(dataSO, path);
			DataSOUtil.SetAddressableAsset(dataSO); // 라벨 type.Name + 주소 type.Name/{ID} (DataLoader 로드 키)
			EditorUtility.SetDirty(dataSO);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private static void EnsureFolder()
		{
			if (AssetDatabase.IsValidFolder(PLANTS_FOLDER) == false)
			{
				AssetDatabase.CreateFolder(PARENT_FOLDER, PLANTS_SUBFOLDER);
			}
		}
	}
}
