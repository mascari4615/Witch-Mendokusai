using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public static class Bootstrap
	{
		[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			// TASK-WM-118 I5 — LoadInstanceFromPreloadAssets() 는 VContainer 가
			// #if UNITY_EDITOR 로만 컴파일(UnityEditor.PlayerSettings = 에디터
			// 전용). player 에선 preloaded asset OnEnable(if isPlaying →
			// Instance=this)이 Instance 셋(TASK-WM-121 [DEBUG-wm121] 로 확인:
			// player 에서 Instance != null).
			if (VContainerSettings.Instance == null)
			{
#if UNITY_EDITOR
				VContainerSettings.LoadInstanceFromPreloadAssets();
#endif
			}

			// TASK-WM-121 — 진짜 standalone player 에서 preloaded
			// VContainerSettings 의 RootLifetimeScope(prefab cross-ref)가 null
			// (확정: [DEBUG-wm121] verdict — Instance != null 인데
			// RootLifetimeScope == null → GetOrCreate 가 VContainer 소스 L72
			// 가드로 silent null 반환 → 조립루트 영구 미빌드 → RootContainerBuilt
			// 미진입 → UIRoot/Lobby NRE → WorldReady 미도달). preloaded SO→prefab
			// 참조가 player 빌드에 안 실리는 Unity 고질. 프리팹은
			// Resources/Singletons 에 있음(WM Singletons-via-Resources 관례,
			// RootLifetimeScope.Configure 의 Resources.Load<SOManager> 와 동형)
			// → ref null 이면 Resources 에서 직접 보강 = fragile cross-ref 의존
			// 제거, player/editor 결정적. WM-118 I5 의 "standalone PASS" 가
			// 에디터 경로였음을 본 fix 가 교정 (WM-117 Tier-B 게이트가 노출).
			VContainerSettings settings = VContainerSettings.Instance;
			if (settings != null && settings.RootLifetimeScope == null)
			{
				UnityEngine.GameObject rootScopePrefabObject =
					UnityEngine.Resources.Load<UnityEngine.GameObject>("Singletons/RootLifetimeScope");
				LifetimeScope rootScopePrefab =
					rootScopePrefabObject != null ? rootScopePrefabObject.GetComponent<LifetimeScope>() : null;
				if (rootScopePrefab != null)
				{
					settings.RootLifetimeScope = rootScopePrefab;
				}
				else
				{
					UnityEngine.Debug.LogError(
						"[Bootstrap] TASK-WM-121 — Resources 'Singletons/RootLifetimeScope' "
						+ "로드 실패. 조립루트 미빌드 → 부팅 불가. 프리팹 경로/Resources 폴더 확인.");
				}
			}

			settings.GetOrCreateRootLifetimeScopeInstance();
		}
	}
}
