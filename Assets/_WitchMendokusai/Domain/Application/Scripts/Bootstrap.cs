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
			if (VContainerSettings.Instance == null)
			{
#if UNITY_EDITOR
				VContainerSettings.LoadInstanceFromPreloadAssets();
#endif
			}
			VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance();
		}
	}
}
