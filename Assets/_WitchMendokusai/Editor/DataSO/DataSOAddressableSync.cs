using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace WitchMendokusai
{
	/// <summary>
	/// DataSO 자산 변경 즉시 Addressable group 자동 sync — TASK-WM-064 Phase A (2026-05-10).
	///
	/// 「Addressable 그룹 .asset 은 DataSO GUID + AssetPrefixes 컨벤션으로 *deterministic 재생성 가능*」 인데
	/// git 정본으로 박혀서 폴더 재구조 / DataSO 추가·삭제 시 *transient drift* 노출 (사용자 발화 「entry 빠진거」, 2026-05-09).
	///
	/// 본 클래스가 세 hook 처리 → `WM/Setup All Addressables` 메뉴 클릭 자체 폐기 (Phase D 격하). 사용자 손 0:
	/// 1. <see cref="OnPostprocessAllAssets"/> — DataSO 자산 import/move 즉시 sync + delete 시 orphan entry 자동 제거 (Phase A + B)
	/// 2. <see cref="MassSyncOnLoad"/> — Editor reload 마다 1회 mass sync (기존 자산 보장 — `[InitializeOnLoadMethod]`)
	/// </summary>
	[InitializeOnLoad]
	public sealed class DataSOAddressableSync : AssetPostprocessor
	{
		[InitializeOnLoadMethod]
		private static void MassSyncOnLoad()
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
				return;

			DataSOUtil.ForeachDataSO(DataSOUtil.SetAddressableAsset, "DataSO Addressable 자동 sync (Editor load)", showDialog: false);
			EditorUtility.SetDirty(settings);
		}


		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
				return;

			bool anyChanged = false;

			List<DataSO> targets = new();
			CollectDataSOs(importedAssets, targets);
			CollectDataSOs(movedAssets, targets);

			foreach (DataSO dataSO in targets)
			{
				if (dataSO == null)
					continue;
				if (DataSOUtil.SetAddressableAsset(dataSO))
					anyChanged = true;
			}

			foreach (string deletedPath in deletedAssets)
			{
				if (string.IsNullOrEmpty(deletedPath))
					continue;
				if (deletedPath.EndsWith(".asset") == false)
					continue;
				if (DataSOUtil.RemoveAddressableEntryByPath(deletedPath))
					anyChanged = true;
			}

			if (anyChanged)
				EditorUtility.SetDirty(settings);
		}

		private static void CollectDataSOs(string[] paths, List<DataSO> output)
		{
			foreach (string path in paths)
			{
				if (string.IsNullOrEmpty(path))
					continue;
				if (path.EndsWith(".asset") == false)
					continue;

				DataSO dataSO = AssetDatabase.LoadAssetAtPath<DataSO>(path);
				if (dataSO == null)
					continue;

				output.Add(dataSO);
			}
		}
	}
}
