using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 씬 사이 검은 막. 부팅 때 한 번 만들어 씬과 무관하게 유지 (UIFPS 와 같은 방식)
	// 나갈 때 덮고 씬 교체, 새 씬 첫 프레임 몇 개 지난 뒤 걷음. 씬 안 전환 (포털, 던전) 은 TransitionView
	public sealed class SceneFader : MonoBehaviour
	{
		// 게임 UI 위, FPS 숫자 (32000) 아래
		private const int SORTING_ORDER = 31000;
		// 실행 중 Hierarchy 의 SceneFader 에서 조절 (DontSave, 숨김 아님)
		[SerializeField] private float coverSeconds = 0.35f;
		[SerializeField] private float revealSeconds = 0.5f;
		// 새 씬 첫 프레임들의 멈칫을 막 뒤에 숨김
		[SerializeField] private int revealDelayFrames = 2;
		[SerializeField] private Color coverColor = new(0.04f, 0.035f, 0.05f, 1f);

		private static SceneFader instance;

		private VisualElement cover;
		private float alpha = 1f;
		private Coroutine running;

		public static bool IsCovering => instance != null && instance.alpha > 0f;

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
			host.hideFlags = HideFlags.DontSave;
			host.AddComponent<SceneFader>();
			host.SetActive(true);
		}

		/// <summary>덮은 뒤 씬 교체. 설치 전 호출이면 바로 교체</summary>
		public static void LoadScene(string sceneName)
		{
			if (instance == null)
			{
				SceneManager.LoadScene(sceneName);
				return;
			}
			instance.Play(instance.CoverThenLoad(sceneName));
		}

		/// <summary>화면을 덮는 코루틴. 비동기 로딩의 씬 활성 직전에 기다림</summary>
		public static IEnumerator Cover()
		{
			if (instance == null)
				yield break;
			yield return instance.FadeTo(1f, instance.coverSeconds);
		}

		private void Awake()
		{
			instance = this;
			UIDocument document = gameObject.AddComponent<UIDocument>();
			document.panelSettings = OverlayPanelSettings.Load();
			document.sortingOrder = SORTING_ORDER;

			VisualElement root = document.rootVisualElement;
			root.pickingMode = PickingMode.Ignore;

			cover = new VisualElement { name = "scene-fader" };
			cover.style.position = Position.Absolute;
			cover.style.left = 0;
			cover.style.top = 0;
			cover.style.right = 0;
			cover.style.bottom = 0;
			cover.style.backgroundColor = coverColor;
			root.Add(cover);
			Apply();

			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			if (instance == this)
				instance = null;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (mode != LoadSceneMode.Single || alpha <= 0f)
				return;
			Play(Reveal());
		}

		private void Play(IEnumerator routine)
		{
			if (running != null)
				StopCoroutine(running);
			running = StartCoroutine(routine);
		}

		private IEnumerator CoverThenLoad(string sceneName)
		{
			yield return FadeTo(1f, coverSeconds);
			running = null;
			SceneManager.LoadScene(sceneName);
		}

		private IEnumerator Reveal()
		{
			for (int i = 0; i < revealDelayFrames; i++)
				yield return null;
			yield return FadeTo(0f, revealSeconds);
			running = null;
		}

		// 시간 정지 (timeScale 0) 중에도 진행
		private IEnumerator FadeTo(float target, float seconds)
		{
			float start = alpha;
			float elapsed = 0f;
			while (elapsed < seconds)
			{
				elapsed += Time.unscaledDeltaTime;
				alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds)));
				Apply();
				yield return null;
			}
			alpha = target;
			Apply();
		}

		private void Apply()
		{
			cover.style.opacity = alpha;
			cover.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
			// 덮는 동안 클릭 차단
			cover.pickingMode = alpha > 0f ? PickingMode.Position : PickingMode.Ignore;
		}

	}
}
