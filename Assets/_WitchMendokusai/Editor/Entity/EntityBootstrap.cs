using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 비지형 entity 부트스트랩. 첫 nature entity (Tree) prefab + EntityData asset 자동 생성.
	/// 임시 모델 — primitive cube 2개 (갈색 줄기 + 초록 잎). 추후 사용자가 모델 교체.
	/// </summary>
	public static class EntityBootstrap
	{
		private const string ENTITY_FOLDER = "Assets/_WitchMendokusai/Content/Entity";
		private const string PREFABS_FOLDER = ENTITY_FOLDER + "/Prefabs";

		[MenuItem("WitchMendokusai/Entity/Generate Default Entities")]
		public static void GenerateDefaultEntities()
		{
			EnsureFolder(ENTITY_FOLDER);
			EnsureFolder(PREFABS_FOLDER);

			GameObject treePrefab = EnsureTreePrefab();
			EnsureEntityAsset(50000000, "나무", treePrefab, EntityCategory.Tree);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			SyncAddressablesGroup();

			Debug.Log("[EntityBootstrap] Default entities ready.");
		}

		[MenuItem("WitchMendokusai/Entity/Sync Addressables Group")]
		public static void SyncAddressablesGroup()
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				Debug.LogError("[EntityBootstrap] AddressableAssetSettings not found.");
				return;
			}

			AddressableAssetGroup group = settings.FindGroup("EntityData");
			if (group == null)
			{
				group = settings.CreateGroup(
					"EntityData",
					setAsDefaultGroup: false,
					readOnly: false,
					postEvent: true,
					schemasToCopy: null,
					typeof(BundledAssetGroupSchema),
					typeof(ContentUpdateGroupSchema));
				Debug.Log("[EntityBootstrap] Addressables group 'EntityData' created.");
			}

			string[] guids = AssetDatabase.FindAssets($"t:{nameof(EntityData)}");
			int registered = 0;
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				EntityData data = AssetDatabase.LoadAssetAtPath<EntityData>(path);
				if (data == null)
					continue;

				AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
				entry.SetAddress($"EntityData/{data.ID}");
				entry.SetLabel("EntityData", true, true);
				registered++;
			}

			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, group, true);
			AssetDatabase.SaveAssets();

			Debug.Log($"[EntityBootstrap] Addressables group 'EntityData' synced. {registered} entries.");
		}

		private static GameObject EnsureTreePrefab()
		{
			string path = $"{PREFABS_FOLDER}/ENT_Tree.prefab";
			GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (existing != null)
				return existing;

			// 임시 모델: 갈색 cube (줄기) + 초록 cube (잎)
			GameObject root = new("ENT_Tree");

			GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
			trunk.name = "Trunk";
			trunk.transform.SetParent(root.transform, false);
			trunk.transform.localPosition = new Vector3(0f, 0.5f, 0f);
			trunk.transform.localScale = new Vector3(0.4f, 1.0f, 0.4f);
			MeshRenderer trunkRenderer = trunk.GetComponent<MeshRenderer>();
			if (trunkRenderer != null && trunkRenderer.sharedMaterial != null)
			{
				Material trunkMat = new(trunkRenderer.sharedMaterial) { color = new Color(0.40f, 0.25f, 0.13f, 1f), name = "TrunkMat" };
				AssetDatabase.CreateAsset(trunkMat, $"{PREFABS_FOLDER}/ENT_Tree_TrunkMat.mat");
				trunkRenderer.sharedMaterial = trunkMat;
			}

			GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
			leaves.name = "Leaves";
			leaves.transform.SetParent(root.transform, false);
			leaves.transform.localPosition = new Vector3(0f, 1.6f, 0f);
			leaves.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
			MeshRenderer leavesRenderer = leaves.GetComponent<MeshRenderer>();
			if (leavesRenderer != null && leavesRenderer.sharedMaterial != null)
			{
				Material leavesMat = new(leavesRenderer.sharedMaterial) { color = new Color(0.25f, 0.55f, 0.20f, 1f), name = "LeavesMat" };
				AssetDatabase.CreateAsset(leavesMat, $"{PREFABS_FOLDER}/ENT_Tree_LeavesMat.mat");
				leavesRenderer.sharedMaterial = leavesMat;
			}

			GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
			Object.DestroyImmediate(root);
			return saved;
		}

		private static EntityData EnsureEntityAsset(int id, string name, GameObject prefab, EntityCategory category)
		{
			string fileName = $"ENT_{id}_{name}";
			string path = $"{ENTITY_FOLDER}/{fileName}.asset";
			EntityData existing = AssetDatabase.LoadAssetAtPath<EntityData>(path);
			if (existing != null)
				return existing;

			EntityData entity = ScriptableObject.CreateInstance<EntityData>();
			entity.ID = id;
			entity.Name = name;
			SerializedObject so = new(entity);
			so.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab;
			so.FindProperty("<Category>k__BackingField").enumValueIndex = (int)category;
			so.ApplyModifiedProperties();

			AssetDatabase.CreateAsset(entity, path);
			return entity;
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;
			string parent = Path.GetDirectoryName(path).Replace("\\", "/");
			string folderName = Path.GetFileName(path);
			if (AssetDatabase.IsValidFolder(parent) == false)
				EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, folderName);
		}
	}
}
