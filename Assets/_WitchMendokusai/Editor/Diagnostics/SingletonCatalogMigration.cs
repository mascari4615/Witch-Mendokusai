using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 조립 목록을 <b>이름 조회에서 참조로</b> 옮긴다 — 한 번 쓰고 지울 이사 도구 (TASK-WM-409 단계 A).
	///
	/// 하는 일:
	///   1. `Core/Resources/Singletons/**` 의 프리팹을 전부 모아 `SingletonCatalog` 에 담는다
	///   2. `SOManager` 도 같이 담는다 (이것도 이름으로 찾던 것)
	///   3. `RootLifetimeScope` 프리팹에 그 카탈로그를 꽂는다
	///
	/// ⚠ 자산은 <b>안 옮긴다</b>. 지금 옮기면 조립 뿌리(`RootLifetimeScope`)가 아직
	///   `Resources/` 에서 로드되므로 참조가 끊긴다. 이사는 단계 B(뿌리를 씬에 배치)와 함께.
	/// </summary>
	public static class SingletonCatalogMigration
	{
		private const string TAG = "[SingletonCatalog]";
		private const string SINGLETONS = "Assets/_WitchMendokusai/Core/Resources/Singletons";
		private const string CATALOG_DIR = "Assets/_WitchMendokusai/Domain/Application/Assets";
		private const string CATALOG = CATALOG_DIR + "/SingletonCatalog.asset";
		private const string ROOT_PREFAB = SINGLETONS + "/RootLifetimeScope.prefab";

		[MenuItem("WM/이사/조립 목록을 참조로 (TASK-WM-409 A)")]
		public static void Run()
		{
			if (AssetDatabase.IsValidFolder(CATALOG_DIR) == false)
			{
				AssetDatabase.CreateFolder("Assets/_WitchMendokusai/Domain/Application", "Assets");
			}

			SingletonCatalog catalog = AssetDatabase.LoadAssetAtPath<SingletonCatalog>(CATALOG);
			if (catalog == null)
			{
				catalog = ScriptableObject.CreateInstance<SingletonCatalog>();
				AssetDatabase.CreateAsset(catalog, CATALOG);
			}

			// 조립 뿌리 자신은 목록에 넣지 않는다 — 자기가 자기를 세우지 않는다.
			List<GameObject> prefabs = new List<GameObject>();
			foreach (string path in Directory.GetFiles(SINGLETONS, "*.prefab", SearchOption.AllDirectories))
			{
				string assetPath = path.Replace('\\', '/');
				if (assetPath.EndsWith("RootLifetimeScope.prefab")) { continue; }
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
				if (prefab != null) { prefabs.Add(prefab); }
			}

			SOManager soManager = null;
			foreach (string guid in AssetDatabase.FindAssets("t:SOManager"))
			{
				soManager = AssetDatabase.LoadAssetAtPath<SOManager>(AssetDatabase.GUIDToAssetPath(guid));
				if (soManager != null) { break; }
			}

			catalog.EditorFill(prefabs.OrderBy(p => p.name).ToArray(), soManager);
			AssetDatabase.SaveAssets();
			Debug.Log(TAG + " 목록 — 프리팹 " + prefabs.Count + " · SOManager " + (soManager == null ? "없음" : soManager.name));

			GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT_PREFAB);
			if (root == null)
			{
				Debug.LogError(TAG + " 조립 뿌리 프리팹을 못 찾았다 — " + ROOT_PREFAB);
				return;
			}

			RootLifetimeScope scope = root.GetComponent<RootLifetimeScope>();
			if (scope == null)
			{
				Debug.LogError(TAG + " RootLifetimeScope 컴포넌트가 없다 — " + ROOT_PREFAB);
				return;
			}

			SerializedObject so = new SerializedObject(scope);
			SerializedProperty prop = so.FindProperty("catalog");
			if (prop == null)
			{
				Debug.LogError(TAG + " catalog 필드를 못 찾았다 — 스크립트 컴파일 상태 확인");
				return;
			}
			prop.objectReferenceValue = catalog;
			so.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(root);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log(TAG + " 배선 완료 — RootLifetimeScope 에 카탈로그를 꽂았다");
		}
	}
}
