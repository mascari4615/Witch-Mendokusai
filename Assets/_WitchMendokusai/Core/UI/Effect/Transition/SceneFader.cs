using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 씬 사이 막. 부팅 때 한 번 만들어 씬과 무관하게 유지 (UIFPS 와 같은 방식)
	// 나갈 때 덮고 씬 교체, 새 씬 첫 프레임 몇 개 지난 뒤 걷음. 씬 안 전환 (포털, 던전) 은 TransitionView
	// 막 모양과 길이는 UIFxSettings (ScreenWipe)
	public sealed class SceneFader : MonoBehaviour
	{
		// 게임 UI 위, FPS 숫자 (32000) 아래
		private const int SORTING_ORDER = 31000;
		// 새 씬 첫 프레임들의 멈칫을 막 뒤에 숨김. 실행 중 Hierarchy (DontDestroyOnLoad) 에서 조절
		[SerializeField] private int revealDelayFrames = 2;
		// UIFxSettings 우클릭 Preview 에서 덮인 채 머무는 시간
		[SerializeField] private float previewHoldSeconds = 0.3f;

		private static SceneFader instance;

		private ScreenWipe wipe;
		private Coroutine running;
		private bool coverOnNextScene;
		private bool loading;

#if !WM_IDLE
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
		private static void Install()
		{
			if (instance != null)
				return;

			GameObject host = new GameObject(nameof(SceneFader));
			host.SetActive(false);
			DontDestroyOnLoad(host);
			host.AddComponent<SceneFader>();
			host.SetActive(true);
		}

		/// <summary>덮은 뒤 씬 교체. 덮는 중 중복 호출은 무시 (키보드 연타)</summary>
		public static void LoadScene(string sceneName)
		{
			// 방치형 빌드 (WM_IDLE) 는 막을 설치하지 않음
			if (instance == null)
			{
				SceneManager.LoadScene(sceneName);
				return;
			}
			if (instance.loading)
				return;
			instance.loading = true;
			instance.Play(instance.CoverThenLoad(sceneName));
		}

		/// <summary>다음 씬 첫 프레임에 막을 바로 씌우고 걷음. 비동기 로딩처럼 교체 시점을 모를 때 사용</summary>
		public static void CoverOnNextScene()
		{
			if (instance != null)
				instance.coverOnNextScene = true;
		}

		/// <summary>미리보기 중인 무늬. 아니면 null (녹화 도구가 읽음)</summary>
		public static ScreenWipeStyle? PreviewingStyle { get; private set; }

		/// <summary>Play 중 무늬 하나를 덮고 걷어 보임. UIFxSettings 우클릭 메뉴</summary>
		public static void PreviewWipe(ScreenWipeStyle wipeStyle)
		{
			if (CanPreview())
				instance.Play(instance.PreviewRoutine(new[] { wipeStyle }));
		}

		/// <summary>Play 중 모든 무늬를 차례로</summary>
		public static void PreviewAllWipes()
		{
			if (CanPreview())
				instance.Play(instance.PreviewRoutine(WipeRenderers.All));
		}

		private static bool CanPreview()
		{
			if (instance != null && instance.loading == false)
				return true;
			Debug.LogWarning("[SceneFader] 미리보기는 Play 중, 씬 교체 중이 아닐 때만");
			return false;
		}

		private void Awake()
		{
			instance = this;
			UIDocument document = gameObject.AddComponent<UIDocument>();
			document.panelSettings = OverlayPanelSettings.Load();
			document.sortingOrder = SORTING_ORDER;

			VisualElement root = document.rootVisualElement;
			root.pickingMode = PickingMode.Ignore;

			wipe = new ScreenWipe(() => UIFxSettings.Instance.sceneWipe);
			root.Add(wipe);
			wipe.SetInstant(true);

			SceneManager.sceneLoaded += OnSceneLoaded;
			// 첫 씬은 sceneLoaded 와 무관하게 걷음. 추가 씬만 열린 Play 에서 검은 화면 고정 방지
			Play(Reveal());
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			wipe?.Dispose();
			PreviewingStyle = null;
			if (instance == this)
				instance = null;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			bool cover = coverOnNextScene;
			coverOnNextScene = false;
			if (mode != LoadSceneMode.Single)
				return;
			loading = false;
			if (cover)
				wipe.SetInstant(true);
			if (wipe.Coverage <= 0f)
				return;
			Play(Reveal());
		}

		private void Play(IEnumerator routine)
		{
			// 미리보기가 다른 흐름에 밀리면 표시도 끝
			PreviewingStyle = null;
			if (running != null)
				StopCoroutine(running);
			running = StartCoroutine(routine);
		}

		private IEnumerator CoverThenLoad(string sceneName)
		{
			bool covered = false;
			wipe.Play(true, () => covered = true);
			while (covered == false)
				yield return null;
			running = null;
			SceneManager.LoadScene(sceneName);
		}

		private IEnumerator PreviewRoutine(ScreenWipeStyle[] styles)
		{
			foreach (ScreenWipeStyle wipeStyle in styles)
			{
				bool covered = false;
				wipe.Play(true, () => covered = true, wipeStyle);
				PreviewingStyle = wipe.CurrentStyle;
				Debug.Log($"[SceneFader] 미리보기 {wipe.CurrentStyle}");
				while (covered == false)
					yield return null;
				yield return new WaitForSecondsRealtime(previewHoldSeconds);
				bool revealed = false;
				wipe.Play(false, () => revealed = true);
				while (revealed == false)
					yield return null;
				yield return new WaitForSecondsRealtime(previewHoldSeconds);
			}
			PreviewingStyle = null;
			running = null;
		}

		private IEnumerator Reveal()
		{
			for (int i = 0; i < revealDelayFrames; i++)
				yield return null;
			wipe.Play(false);
			running = null;
		}
	}
}
