using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using static WitchMendokusai.DataSODefine;

namespace WitchMendokusai
{
	public static class DataSOUtil
	{
		public static bool TryGetBaseType(DataSO dataSO, out Type baseType)
		{
			Type type = dataSO.GetType();
			while (type != typeof(DataSO) && AssetPrefixes.ContainsKey(type) == false)
				type = type.BaseType;

			baseType = type;
			return baseType != typeof(DataSO);
		}

		public static string GetCorrectAssetName(DataSO dataSO)
		{
			if (TryGetBaseType(dataSO, out Type type) == false)
			{
				Debug.LogError("Base type not found");
				return null;
			}
			return ConvertToCorrectAssetName($"{AssetPrefixes[type]}_{dataSO.ID}_{dataSO.Name}");
		}

		public static string ConvertToCorrectAssetName(string name)
		{
			// 허용되지 않는 문자 정의
			const string notAllowedChars = "\"\'\\/:*?<>|[],. ";
			
			// 각 문자 제거
			foreach (char c in notAllowedChars)
			{
				name = name.Replace(c.ToString(), string.Empty);
			}
			
			// 추가로 시스템 파일명 제한 문자 제거
			foreach (char c in Path.GetInvalidFileNameChars())
			{
				name = name.Replace(c.ToString(), string.Empty);
			}
			
			return name;
		}

		public static void SetCorrectAssetName(DataSO dataSO)
		{
			string goodName = GetCorrectAssetName(dataSO);

			if (dataSO.name.Equals(goodName))
				return;

			AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(dataSO), goodName);
			EditorUtility.SetDirty(dataSO);
			AssetDatabase.SaveAssets();
		}

		#region Save
		[MenuItem("WitchMendokusai/SaveAssets")]
		public static void SaveAssets()
		{
			ForeachDataSO(SetDirty, "SaveAssets");

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		public static bool SetDirty(DataSO dataSO)
		{
			EditorUtility.SetDirty(dataSO);
			return true;
		}

		public static void SaveAsset(DataSO dataSO)
		{
			SetCorrectAssetName(dataSO);

			EditorUtility.SetDirty(dataSO);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
		#endregion

		#region Addressable
		// 그룹 캐싱을 위한 정적 딕셔너리
		private static readonly Dictionary<string, AddressableAssetGroup> addressableGroups = new();

		[MenuItem("WitchMendokusai/Setup All Addressables")]
		public static void SetupAllAddressables()
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				Debug.LogError("Addressable Asset Settings not found");
				return;
			}

			ForeachDataSO(SetAddressableAsset, "Addressable 설정");

			EditorUtility.SetDirty(settings);
		}

		public static bool SetAddressableAsset(DataSO dataSO)
		{
			if (TryGetBaseType(dataSO, out Type type) == false)
			{
				Debug.LogError($"Base type not found for {dataSO.name}");
				return false;
			}

			// Addressable Asset Settings 가져오기
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				Debug.LogError("Addressable Asset Settings not found");
				return false;
			}

			// settings에 라벨이 있는지 확인
			if (settings.GetLabels().Find(label => label == type.Name) == null)
			{
				Debug.Log($"Add label: {type.Name}");
				settings.AddLabel(type.Name);
			}

			// 에셋의 GUID 가져오기
			string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dataSO));

			// 이미 Addressable로 등록되어 있는지 확인
			AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
			if (existingEntry != null)
			{
				// 다른 라벨이 있다면 지우기 (오래된 라벨)
				foreach (string label in existingEntry.labels.ToList())
				{
					if (label != type.Name)
						existingEntry.labels.Remove(label);
				}

				// 이미 올바른 주소 형식을 가지고 있는지 확인
				string expectedAddress = $"{type.Name}/{dataSO.ID}";
				if (existingEntry.address == expectedAddress && existingEntry.labels.Contains(type.Name))
				{
					// 이미 올바르게 설정되어 있음
					return true;
				}

				// 주소나 라벨이 다르면 업데이트
				existingEntry.address = expectedAddress;

				// 라벨 추가
				if (existingEntry.labels.Contains(type.Name) == false)
				{
					existingEntry.labels.Add(type.Name);
				}

				settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, existingEntry, true);
				return true;
			}

			// 없는 경우

			// 그룹 이름 (데이터 타입에 따라 다른 그룹 설정)
			string groupName = $"{type.Name}";

			// 그룹 캐싱 (성능 향상을 위해 정적 딕셔너리 사용)
			if (addressableGroups.TryGetValue(groupName, out AddressableAssetGroup group) == false)
			{
				group = settings.FindGroup(groupName);
				if (group == null)
				{
					group = settings.CreateGroup(groupName, false, false, true,
						new List<AddressableAssetGroupSchema>
						{
							settings.DefaultGroup.GetSchema<ContentUpdateGroupSchema>(),
							settings.DefaultGroup.GetSchema<BundledAssetGroupSchema>()
						});
				// TODO: 생성하고 바로 등록하면 안되는 것 같음. (밑에 entry == null 나오면서 에러 발생, 다시 InitDict하면 설정됨)
				}
				addressableGroups[groupName] = group;
			}

			// Addressable 에셋으로 등록
			AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
			if (entry == null)
			{
				// 실패: 이미 다른 주소로 등록되어 있는 경우?
				Debug.LogError($"Failed to create Addressable entry for {dataSO.name} ({AssetDatabase.GetAssetPath(dataSO)}, {guid}, {type.Name})");
				return false;
			}

			// 주소 설정 - 타입/ID 형식
			entry.address = $"{type.Name}/{dataSO.ID}";

			// 레이블 추가
			entry.labels.Add(type.Name);

			// Addressable 설정 저장
			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);

			return true;
		}
		#endregion

		public static void ForeachDataSO(Func<DataSO, bool> action, string taskName = "무언가", bool showDialog = true)
		{
			if ((showDialog == false) || EditorUtility.DisplayDialog($"{taskName}", $"모든 DataSO에 {taskName}을 적용하시겠습니까?", "예", "아니오"))
			{
				try
				{
					int successCount = 0;
					int nonTargetCount = 0;

					// 에셋 검색 시작
					string[] guids = AssetDatabase.FindAssets("t:DataSO", new[] { BASE_DIR });

					// 진행 상황 표시
					EditorUtility.DisplayProgressBar($"{taskName} 중", "DataSO 에셋을 처리하고 있습니다", 0f);

					for (int i = 0; i < guids.Length; i++)
					{
						string guid = guids[i];
						string path = AssetDatabase.GUIDToAssetPath(guid);
						DataSO dataSO = AssetDatabase.LoadAssetAtPath<DataSO>(path);

						if (TryGetBaseType(dataSO, out Type type) == false)
						{
							// Debug.LogError($"목표로 하는 타입이 아닙니다: {dataSO.name}, {dataSO.GetType()}");
							nonTargetCount++;
							continue;
						}

						if (dataSO != null)
						{
							bool result = action.Invoke(dataSO);
							if (result == true)
							{
								successCount++;
							}
						}

						EditorUtility.DisplayProgressBar($"{taskName} 중", $"{i + 1}/{guids.Length} 처리 중...", (float)i / guids.Length);
					}

					EditorUtility.ClearProgressBar();

					int targetCount = guids.Length - nonTargetCount;
					float successRate = targetCount > 0 ? successCount / (float)targetCount : 0f;
					string detail = $"[{successCount}/{targetCount}( = {guids.Length} - {nonTargetCount} )]";
					Debug.Log($"{successRate:P} = {detail} | 총 {successCount}개의 DataSO에 {taskName}을 적용했습니다.");
				}
				catch (Exception ex)
				{
					EditorUtility.ClearProgressBar();
					Debug.LogError($"{taskName} 중 오류 발생: {ex.Message}");
				}
			}
		}
	}
}