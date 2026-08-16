using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 조립 뿌리를 <b>참조로</b> 들고 다니게 한다 — `BootConfig` 생성 + preloaded 등록 (TASK-WM-409 B).
	///
	/// ★ preloaded assets = 씬과 무관하게 빌드에 실리고 시작 때 로드되는 자산 목록.
	///   여기에 SO 를 넣고 그 SO 가 프리팹을 참조하면 <c>Resources/</c> 가 필요 없다.
	///   ⚠ `TASK-WM-121` 이 「preloaded SO → prefab 참조가 player 에서 null」 고질을 적어 뒀다.
	///     그래서 이 도구는 배선만 하고, <b>진짜 사는지는 부팅 스모크가 판정</b>한다.
	/// </summary>
	public static class BootConfigMigration
	{
		private const string TAG = "[BootConfig]";
		private const string DIR = "Assets/_WitchMendokusai/Domain/Application/Assets";
		private const string CONFIG = DIR + "/BootConfig.asset";
		private const string ROOT_PREFAB = "Assets/_WitchMendokusai/Core/Resources/Singletons/RootLifetimeScope.prefab";

		[MenuItem("WM/이사/조립 뿌리를 참조로 (TASK-WM-409 B)")]
		public static void Run()
		{
			BootConfig config = AssetDatabase.LoadAssetAtPath<BootConfig>(CONFIG);
			if (config == null)
			{
				config = ScriptableObject.CreateInstance<BootConfig>();
				AssetDatabase.CreateAsset(config, CONFIG);
			}

			GameObject rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT_PREFAB);
			if (rootPrefab == null)
			{
				Debug.LogError(TAG + " 조립 뿌리 프리팹을 못 찾았다 — " + ROOT_PREFAB);
				return;
			}
			LifetimeScope scope = rootPrefab.GetComponent<LifetimeScope>();
			if (scope == null)
			{
				Debug.LogError(TAG + " LifetimeScope 컴포넌트가 없다 — " + ROOT_PREFAB);
				return;
			}

			SerializedObject so = new SerializedObject(config);
			SerializedProperty prop = so.FindProperty("rootScopePrefab");
			prop.objectReferenceValue = scope;
			so.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(config);

			// preloaded 목록에 넣는다 (중복 없이).
			List<Object> preloaded = PlayerSettings.GetPreloadedAssets().Where(a => a != null).ToList();
			if (preloaded.Contains(config) == false)
			{
				preloaded.Add(config);
				PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
				Debug.Log(TAG + " preloaded 에 등록했다 (총 " + preloaded.Count + ")");
			}
			else
			{
				Debug.Log(TAG + " 이미 preloaded 에 있다");
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log(TAG + " 배선 완료 — 부팅 스모크 로그에서 「참조로 조립 뿌리를 찾았다」가 찍히는지 볼 것");
		}
	}
}
