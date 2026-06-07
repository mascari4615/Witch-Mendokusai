using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.NetworkEditor
{
	/// <summary>
	/// TASK-WM-187 — WorldClockNetworkBridge prefab idempotent 자동 부트스트랩.
	///
	/// FishNet `DefaultPrefabObjects.asset` 가 비어 있으면 NetworkManager 가 OnStartServer 호출 경로를 못 엮음
	/// (spec: grep prefab 0 → OnStartServer 영영 미발화). 본 InitializeOnLoad 가:
	///   1. prefab 자산 누락 시 NetworkObject + WorldClockNetworkBridge 부착 prefab 생성
	///   2. DefaultPrefabObjects._prefabs 에 등록 누락이면 추가
	/// idempotent — 반복 호출해도 안전. WorldClockBootstrapMenu 와 동일 패턴(WM-054-A 패턴).
	///
	/// 게임 부팅 경로(World scene + NetworkManager 실제 띄울 때) 가 본 prefab 을 spawn 해 OnStartServer 발화 = 실 WorldClock 동기 채널.
	/// 호스트 PlayVerify(<see cref="WMNetSyncHostPlayVerify"/>) 는 prefab 의존성 없는 격리 sync smoke (smoke ⊥ 실 동기).
	/// </summary>
	public static class WorldClockNetworkBridgePrefabBootstrap
	{
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Network/Resources/WorldClockNetworkBridge.prefab";
		private const string DEFAULT_PREFAB_OBJECTS_PATH = "Assets/DefaultPrefabObjects.asset";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (prefab == null)
				prefab = CreatePrefab();

			RegisterInDefaultPrefabObjects(prefab);
		}

		[MenuItem("WM/Setup/Recreate WorldClockNetworkBridge Prefab")]
		private static void RecreateMenuItem()
		{
			GameObject prefab = CreatePrefab();
			RegisterInDefaultPrefabObjects(prefab);
		}

		private static GameObject CreatePrefab()
		{
			EnsureParentFolders(PREFAB_PATH);

			GameObject root = new GameObject(nameof(WorldClockNetworkBridge));
			root.AddComponent<NetworkObject>();
			root.AddComponent<WorldClockNetworkBridge>();

			GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log("[WorldClockNetworkBridgePrefabBootstrap] Created " + PREFAB_PATH);
			return saved;
		}

		// "Assets/A/B/x.prefab" → A, B 폴더가 없으면 만든다. SaveAsPrefabAsset 가 폴더 누락 시 실패하는 회피.
		private static void EnsureParentFolders(string assetPath)
		{
			string[] parts = assetPath.Split('/');
			if (parts.Length < 3 || parts[0] != "Assets")
				return;

			string current = "Assets";
			for (int i = 1; i < parts.Length - 1; i++)
			{
				string next = current + "/" + parts[i];
				if (AssetDatabase.IsValidFolder(next) == false)
					AssetDatabase.CreateFolder(current, parts[i]);
				current = next;
			}
		}

		private static void RegisterInDefaultPrefabObjects(GameObject prefab)
		{
			if (prefab == null)
				return;

			NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
			if (networkObject == null)
				return;

			DefaultPrefabObjects collection =
				AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DEFAULT_PREFAB_OBJECTS_PATH);
			if (collection == null)
				return;

			if (collection.Prefabs != null && collection.Prefabs.Contains(networkObject) == true)
				return;

			collection.AddObject(networkObject, true);
			EditorUtility.SetDirty(collection);
			AssetDatabase.SaveAssetIfDirty(collection);

			Debug.Log("[WorldClockNetworkBridgePrefabBootstrap] Registered "
				+ prefab.name + " in " + DEFAULT_PREFAB_OBJECTS_PATH);
		}
	}
}
