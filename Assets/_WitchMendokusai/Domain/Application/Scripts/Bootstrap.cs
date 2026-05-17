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
			// #if UNITY_EDITOR 로만 컴파일(UnityEditor.PlayerSettings 사용 = 에디터
			// 전용). 무가드 호출 = 에디터 통과 / player 빌드 CS0117 (WM 가 여태
			// player 빌드 0이라 잠복). 패키지 자체도 동일 에디터 전용
			// [RuntimeInitializeOnLoadMethod] 로 이걸 호출(주석 "For editor, we
			// need to load the Preload asset manually") — player 에선 preloaded
			// asset 의 OnEnable(if isPlaying → Instance=this)을 Unity 가 자동 처리
			// 하므로 수동 호출 불요. 가드 = VContainer 설계 패턴과 동형.
			// [DEBUG-wm121] TASK-WM-121 — player 조립루트 미빌드 sub-path 판별.
			// 단일 grep cleanup. 동작 무변경(로깅만 + 기존 호출 유지).
			UnityEngine.Debug.Log("[DEBUG-wm121] Bootstrap.OnBooting 진입");
			UnityEngine.Debug.Log($"[DEBUG-wm121] pre: Instance null? {VContainerSettings.Instance == null}");
			if (VContainerSettings.Instance == null)
			{
#if UNITY_EDITOR
				VContainerSettings.LoadInstanceFromPreloadAssets();
#endif
			}
			UnityEngine.Debug.Log($"[DEBUG-wm121] post-#if: Instance null? {VContainerSettings.Instance == null}");
			if (VContainerSettings.Instance != null)
			{
				UnityEngine.Debug.Log($"[DEBUG-wm121] RootLifetimeScope ref null? {VContainerSettings.Instance.RootLifetimeScope == null}");
			}
			VContainer.Unity.LifetimeScope wm121Scope =
				VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance();
			UnityEngine.Debug.Log($"[DEBUG-wm121] GetOrCreate 반환 null? {wm121Scope == null} "
				+ $"container null? {(wm121Scope == null ? "n/a" : (wm121Scope.Container == null).ToString())}");
		}
	}
}
