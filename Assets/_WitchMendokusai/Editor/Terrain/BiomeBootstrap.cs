using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// BiomeData entitySpawns 기본값 시드.
	/// EntityBootstrap이 만든 Tree(ID 50000000)를 Forest / Plains 바이옴에 spawn rule로 backfill.
	/// 이미 같은 entity rule 있으면 density만 갱신, 없으면 추가 (멱등).
	/// </summary>
	public static class BiomeBootstrap
	{
		private const int TREE_ID = 50000000;
		private const float FOREST_TREE_DENSITY = 0.05f;
		private const float PLAINS_TREE_DENSITY = 0.02f;

		[MenuItem("WitchMendokusai/Biome/Backfill Default Entity Spawns")]
		public static void BackfillDefaultEntitySpawns()
		{
			// 자연 entity 자산이 없으면 먼저 시드 (멱등 — 이미 있으면 no-op)
			EntityBootstrap.GenerateDefaultEntities();

			EntityData tree = LoadEntityById(TREE_ID);
			if (tree == null)
			{
				Debug.LogError($"[BiomeBootstrap] EntityData ID {TREE_ID} (Tree) not found after EntityBootstrap. Aborting.");
				return;
			}

			int updated = 0;
			if (BackfillBiome("Forest", tree, FOREST_TREE_DENSITY))
				updated++;
			if (BackfillBiome("Plains", tree, PLAINS_TREE_DENSITY))
				updated++;

			AssetDatabase.SaveAssets();
			Debug.Log($"[BiomeBootstrap] Backfill done. {updated} biomes updated.");
		}

		private static EntityData LoadEntityById(int id)
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(EntityData)}");
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				EntityData data = AssetDatabase.LoadAssetAtPath<EntityData>(path);
				if (data != null && data.ID == id)
					return data;
			}
			return null;
		}

		private static BiomeData LoadBiomeByName(string biomeName)
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(BiomeData)}");
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				BiomeData biome = AssetDatabase.LoadAssetAtPath<BiomeData>(path);
				if (biome != null && biome.BiomeName == biomeName)
					return biome;
			}
			return null;
		}

		private static bool BackfillBiome(string biomeName, EntityData entity, float density)
		{
			BiomeData biome = LoadBiomeByName(biomeName);
			if (biome == null)
			{
				Debug.LogWarning($"[BiomeBootstrap] Biome '{biomeName}' not found. Skipping.");
				return false;
			}

			SerializedObject serializedObject = new(biome);
			SerializedProperty list = serializedObject.FindProperty("entitySpawns");
			if (list == null)
			{
				Debug.LogError($"[BiomeBootstrap] '{biomeName}': entitySpawns property not found. BiomeData 컴파일 확인.");
				return false;
			}

			int existingIndex = -1;
			for (int i = 0; i < list.arraySize; i++)
			{
				SerializedProperty element = list.GetArrayElementAtIndex(i);
				Object referenced = element.FindPropertyRelative("entity").objectReferenceValue;
				if (referenced == entity)
				{
					existingIndex = i;
					break;
				}
			}

			if (existingIndex < 0)
			{
				list.InsertArrayElementAtIndex(list.arraySize);
				SerializedProperty newElement = list.GetArrayElementAtIndex(list.arraySize - 1);
				newElement.FindPropertyRelative("entity").objectReferenceValue = entity;
				newElement.FindPropertyRelative("density").floatValue = density;
			}
			else
			{
				SerializedProperty element = list.GetArrayElementAtIndex(existingIndex);
				element.FindPropertyRelative("density").floatValue = density;
			}

			serializedObject.ApplyModifiedProperties();
			EditorUtility.SetDirty(biome);
			return true;
		}
	}
}
