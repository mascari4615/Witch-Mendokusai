using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace WitchMendokusai
{
	public static class MDataSOUtil
	{
		public const string BASE_DIR = "Assets/_WitchMendokusai/";
		public const string EDITOR_DIR = BASE_DIR + "Editor/";
		public const int ID_MAX = 100_000_000;

		public static readonly Dictionary<Type, string> AssetPrefixes = new()
		{
			{ typeof(QuestSO), "Q" },
			{ typeof(CardData), "C" },
			{ typeof(ItemData), "I" },
			{ typeof(MonsterWave), "MW" },
			{ typeof(ObjectData), "O"},
			{ typeof(SkillData), "SKL" },
			{ typeof(UnitStatData), "USD"},
			{ typeof(GameStatData), "GSD"},
			{ typeof(WorldStage), "WS" },
			{ typeof(Dungeon), "D" },
			{ typeof(DungeonStage), "DS" },
			{ typeof(DungeonStatData), "DSD" },
			{ typeof(DungeonConstraint), "DC" },
			{ typeof(Doll), "DOL" },
			{ typeof(NPC), "NPC" },
			{ typeof(Monster), "MOB" },
			{ typeof(Building), "B"}
		};

		public static Type GetBaseType(DataSO dataSO)
		{
			Type type = dataSO.GetType();
			while (type != typeof(DataSO) && AssetPrefixes.ContainsKey(type) == false)
				type = type.BaseType;

			return type;
		}

		// 이 아래부터는 Copilot가 만들어준 코드.

		// 그룹 캐싱을 위한 정적 딕셔너리
		private static Dictionary<string, AddressableAssetGroup> addressableGroups = new Dictionary<string, AddressableAssetGroup>();

		// 일괄 처리를 위한 메뉴 아이템 추가
		[MenuItem("WitchMendokusai/Setup All Addressables")]
		public static void SetupAllAddressables()
		{
			if (EditorUtility.DisplayDialog("Addressable 설정", "모든 DataSO에 Addressable 설정을 적용하시겠습니까?", "예", "아니오"))
			{
				try
				{
					int count = 0;
					AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
					if (settings == null)
					{
						Debug.LogError("Addressable Asset Settings not found");
						return;
					}

					// 그룹 준비 작업
					Dictionary<string, AddressableAssetGroup> groups = new();

					// 에셋 검색 시작
					string[] guids = AssetDatabase.FindAssets("t:DataSO", new[] { BASE_DIR });

					// 진행 상황 표시
					EditorUtility.DisplayProgressBar("Addressable 설정 중", "DataSO 에셋을 처리하고 있습니다", 0f);

					for (int i = 0; i < guids.Length; i++)
					{
						string guid = guids[i];
						string path = AssetDatabase.GUIDToAssetPath(guid);
						DataSO dataSO = AssetDatabase.LoadAssetAtPath<DataSO>(path);

						if (dataSO != null)
						{
							SetAddressableAsset(dataSO, path);
							count++;
						}

						EditorUtility.DisplayProgressBar("Addressable 설정 중", $"{i + 1}/{guids.Length} 처리 중...", (float)i / guids.Length);
					}

					EditorUtility.ClearProgressBar();
					Debug.Log($"{count}개의 DataSO에 Addressable 설정을 적용했습니다.");
				}
				catch (Exception ex)
				{
					EditorUtility.ClearProgressBar();
					Debug.LogError($"Addressable 설정 중 오류 발생: {ex.Message}");
				}
			}
		}
		public static bool SetAddressableAsset(DataSO dataSO, string path)
		{
			Type type = GetBaseType(dataSO);

			// Addressable Asset Settings 가져오기
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				Debug.LogError("Addressable Asset Settings not found");
				return false;
			}

			// 에셋의 GUID 가져오기
			string guid = AssetDatabase.AssetPathToGUID(path);

			// 이미 Addressable로 등록되어 있는지 확인
			AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
			if (existingEntry != null)
			{
				// 이미 올바른 주소 형식을 가지고 있는지 확인
				string expectedAddress = $"{type.Name}/{dataSO.ID}";
				if (existingEntry.address == expectedAddress && existingEntry.labels.Contains(type.Name))
				{
					// 이미 올바르게 설정되어 있음
					return true;
				}

				// 주소나 라벨이 다르면 업데이트
				existingEntry.address = expectedAddress;
				if (!existingEntry.labels.Contains(type.Name))
					existingEntry.labels.Add(type.Name);

				settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, existingEntry, true);
				return true;
			}

			// 그룹 이름 (데이터 타입에 따라 다른 그룹 설정)
			string groupName = $"{type.Name}";

			// 그룹 캐싱 (성능 향상을 위해 정적 딕셔너리 사용)
			if (!addressableGroups.TryGetValue(groupName, out AddressableAssetGroup group))
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
				}
				addressableGroups[groupName] = group;
			}

			// Addressable 에셋으로 등록
			AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);

			if (entry == null)
			{
				Debug.LogError($"Failed to create Addressable entry for {dataSO.name}");
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
	}
}