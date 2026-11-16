using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	// 화면 왼쪽 위 FPS 숫자. 씬과 무관하게 부팅 때 한 번 만들어 계속 유지 (인트로, 시작 화면, 월드, 던전)
	public sealed class UIFPS : MonoBehaviour
	{
		private const string VISIBLE_PREF_KEY = "WM.Display.Fps";
		// 빌드 표시 (BuildStampOverlay) 와 게임 UI 위
		private const int SORTING_ORDER = 32000;
		// 월드 왼쪽 위 세계 시계 아래 자리, 패널 기준 해상도 픽셀
		private const float LEFT = 6f;
		private const float TOP = 30f;
		private const float FONT_SIZE = 14f;
		// 프레임 시간 지수 평활 비율
		private const float SMOOTHING = 0.1f;
		// 밝은 하늘 (시작 화면) 위에서도 읽히게 진한 흰색과 검은 테두리
		private static readonly Color TEXT_COLOR = new(1f, 1f, 1f, 0.9f);
		private static readonly Color OUTLINE_COLOR = new(0f, 0f, 0f, 0.8f);
		private const float OUTLINE_WIDTH = 1f;

		private static bool? isVisible;
		// 도메인 재로드 없는 Play 재시작에서도 다시 만들도록 bool 대신 실제 객체로 판정
		private static UIFPS instance;

		// 환경설정 시스템 항목과 개발자 창 표시 탭이 같은 값을 씀. 릴리스 빌드 포함, 재실행 뒤 유지
		public static bool IsVisible
		{
			get => isVisible ??= PlayerPrefs.GetInt(VISIBLE_PREF_KEY, 0) == 1;
			set
			{
				isVisible = value;
				PlayerPrefs.SetInt(VISIBLE_PREF_KEY, value ? 1 : 0);
				PlayerPrefs.Save();
			}
		}

#if !WM_IDLE
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
		private static void Install()
		{
			if (instance != null)
				return;

			GameObject host = new GameObject(nameof(UIFPS));
			host.SetActive(false);
			DontDestroyOnLoad(host);
			host.hideFlags = HideFlags.HideAndDontSave;
			host.AddComponent<UIFPS>();
			host.SetActive(true);
		}

		private VisualElement safeFrame;
		private Rect safeArea = SafeArea.UNSET;
		private Label label;
		private float deltaTime;

		private void Awake()
		{
			instance = this;
			UIDocument document = gameObject.AddComponent<UIDocument>();
			document.panelSettings = OverlayPanelSettings.Load();
			document.sortingOrder = SORTING_ORDER;

			VisualElement root = document.rootVisualElement;
			root.pickingMode = PickingMode.Ignore;

			safeFrame = new VisualElement { name = "fps-safe-frame" };
			safeFrame.style.position = Position.Absolute;
			safeFrame.pickingMode = PickingMode.Ignore;
			root.Add(safeFrame);

			label = new Label { name = "fps" };
			label.pickingMode = PickingMode.Ignore;
			label.style.position = Position.Absolute;
			label.style.left = LEFT;
			label.style.top = TOP;
			label.style.fontSize = FONT_SIZE;
			label.style.color = TEXT_COLOR;
			label.style.unityTextOutlineColor = OUTLINE_COLOR;
			label.style.unityTextOutlineWidth = OUTLINE_WIDTH;
			safeFrame.Add(label);
		}


		private void Update()
		{
			if (safeFrame != null && SafeArea.Refresh(ref safeArea))
				SafeArea.ApplyInsets(safeFrame, safeArea);

			bool visible = IsVisible;
			label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			if (visible == false)
				return;

			deltaTime += (Time.unscaledDeltaTime - deltaTime) * SMOOTHING;
			label.text = Mathf.Ceil(1.0f / deltaTime).ToString();
		}
	}
}
