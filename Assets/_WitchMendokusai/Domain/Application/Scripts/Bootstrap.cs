using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public static class Bootstrap
	{
		/// <summary>본편이 아닌 씬 — 여기서는 본편 조립을 아예 안 세운다.</summary>
		private const string SIDE_GAME_SCENE = "IdleV2";

		[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			// ★ 방치형(`Idle`)은 <b>따로 파는 게임</b>이다 — 본편 조립·데이터·로비가 필요 없다.
			//   여기서 안 막으면 본편 뿌리가 서고, 그 안의 `DataLoader` 가
			//   「로딩 시 강제로 로비로 이동」을 실행해 <b>다른 게임이 시작된다</b>(실제로 겪었다).
			//   빌드에서는 이 어셈블리 자체가 안 실리지만(`WM_IDLE`), 에디터에는 표식이 없다.
			//   하나씩 막지 않고 <b>뿌리에서</b> 막는다 — 스스로 뜨는 것이 스물세 곳이라 하나씩은 못 막는다.
			if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SIDE_GAME_SCENE)
			{
				return;
			}

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
				// ★ 참조 우선 (TASK-WM-409 B) — preloaded `BootConfig` 가 조립 뿌리를 <b>참조로</b> 들고 있다.
				//   이름 조회(Resources)는 그 다음이다. 이 순서가 뒤집히면 `Resources/Singletons` 가
				//   <b>모든 제품 빌드</b>에 계속 실린다.
				LifetimeScope rootScopePrefab = null;
				BootConfig bootConfig = BootConfig.Live;
				if (bootConfig != null && bootConfig.RootScopePrefab != null)
				{
					rootScopePrefab = bootConfig.RootScopePrefab;
					UnityEngine.Debug.Log("[BootConfig] 참조로 조립 뿌리를 찾았다 (Resources 안 씀)");
				}

				if (rootScopePrefab == null)
				{
					// ⚠ 폴백 — WM-121 이 적어 둔 유니티 고질(preloaded SO→prefab 참조가 player 에서 null)
					//   때문에 남긴다. 이 줄이 <b>실제로 필요한지</b>는 부팅 스모크가 답한다:
					//   위 로그가 찍히면 필요 없고, 안 찍히면 아직 필요하다.
					UnityEngine.GameObject rootScopePrefabObject =
						UnityEngine.Resources.Load<UnityEngine.GameObject>("Singletons/RootLifetimeScope");
					rootScopePrefab =
						rootScopePrefabObject != null ? rootScopePrefabObject.GetComponent<LifetimeScope>() : null;
					if (rootScopePrefab != null)
					{
						UnityEngine.Debug.LogWarning("[BootConfig] 참조가 죽어 Resources 로 되돌아갔다 (TASK-WM-409 측정 대상)");
					}
				}
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
