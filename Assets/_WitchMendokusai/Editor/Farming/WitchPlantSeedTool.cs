using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 마도 온실 「봐줘야 진짜가 된다」 루프가 Codex(박물관)에 보이려면 *등록된* WitchPlantSO 종이 최소 1개 필요하다.
	// 파이프라인: DataLoader 가 Addressable label("WitchPlantSO")로 로드 → SOManager.DataSOs → PlantCodexCategory 나열.
	// 종 .asset 이 0개면 도감이 텅 비어 "수확했는데 아무것도 없다"(TASK-WM-167 1f+ 루프 단절). 이 도구가 그 최소 1개를
	// 멱등 보장 + canon Addressable 등록(DataSOUtil.SetAddressableAsset = 라벨 type.Name + 주소 type.Name/ID)을 한다.
	//
	// ★ 종 식별(디제틱 이름·스프라이트·성장/시듦 수치)은 사용자 Grey Box(MDD — Yon 이 손볼 영역). 샘플은 placeholder —
	//   WM/DataSOWindow 에서 개명·재튜닝하거나 WM/Farming/WitchPlantSO(CreateAssetMenu)로 종을 추가하면 자동 로드된다.
	// process.md § 모든 자동화는 수동 트리거 전제 = MenuItem 동반(부트 자동 시드 X — 디자이너 통제 유지).
	public static class WitchPlantSeedTool
	{
		private const string PARENT_FOLDER = "Assets/_WitchMendokusai/Domain/Farming";
		private const string PLANTS_SUBFOLDER = "Plants";
		private const string PLANTS_FOLDER = PARENT_FOLDER + "/" + PLANTS_SUBFOLDER;

		// placeholder 디제틱 이름 — 마도 작물(시드는 종). 사용자가 DataSOWindow 로 개명.
		private const string SAMPLE_PLANT_NAME = "달빛이끼";
		private const int SAMPLE_PLANT_ID = 0;

		[MenuItem("WM/Farming/Ensure Sample Plant")]
		public static WitchPlantSO EnsureSamplePlant()
		{
			// 멱등 — 종이 하나라도 있으면 새로 만들지 않고 첫 종의 Addressable 등록만 보강(라벨 누락 방어).
			string[] existingGuids = AssetDatabase.FindAssets($"t:{nameof(WitchPlantSO)}");
			if (existingGuids.Length > 0)
			{
				WitchPlantSO found = AssetDatabase.LoadAssetAtPath<WitchPlantSO>(AssetDatabase.GUIDToAssetPath(existingGuids[0]));
				DataSOUtil.SetAddressableAsset(found);
				AssetDatabase.SaveAssets();
				Debug.Log($"[WitchPlantSeed] 이미 {existingGuids.Length}종 존재 — 첫 종 Addressable 보강: '{found.Name}'(ID {found.ID})");
				return found;
			}

			if (AssetDatabase.IsValidFolder(PLANTS_FOLDER) == false)
			{
				AssetDatabase.CreateFolder(PARENT_FOLDER, PLANTS_SUBFOLDER);
			}

			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults(); // CreateInstance 는 [field: SerializeField] 이니셜라이저를 0으로 덮음 → 합리 기본값 재주입.
			plant.ID = SAMPLE_PLANT_ID;
			plant.Name = SAMPLE_PLANT_NAME;

			string assetName = DataSOUtil.ConvertToCorrectAssetName($"{DataSODefine.AssetPrefixes[typeof(WitchPlantSO)]}_{plant.ID}_{plant.Name}");
			string path = AssetDatabase.GenerateUniqueAssetPath($"{PLANTS_FOLDER}/{assetName}.asset");
			AssetDatabase.CreateAsset(plant, path);

			// canon Addressable 등록 — 라벨 "WitchPlantSO" + 주소 "WitchPlantSO/0" (DataLoader 가 이 라벨로 로드).
			DataSOUtil.SetAddressableAsset(plant);

			EditorUtility.SetDirty(plant);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[WitchPlantSeed] 샘플 종 생성: {path} (ID {plant.ID}, '{plant.Name}') + Addressable 등록 완료 — 도감에 표본으로 나열 가능.");
			return plant;
		}
	}
}
