using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴. ESC 와 폰 뒤로가기, 폰 메뉴 버튼으로 여는 화면. PC 와 폰 공통
	/// - 왼쪽: 자주 여는 창, 오른쪽: 정보 창, 아래: 설정과 종료, 가운데: 지금 자리와 연구소 코어
	/// - 설정창은 이 안의 버튼 하나. 사용자가 설정을 닫으면 메뉴로 돌아옴
	/// - 창 단축키가 이 빌드에서 숨는 항목 (KeybindRegistry) 은 메뉴에서도 숨김
	/// - World 씬에서만 열림. 시작 화면의 ESC 는 설정창 (LobbyManager)
	/// 시안과 결정: memo wm/design/esc-menu-20261117
	/// </summary>
	public partial class FullMenuView : MonoBehaviour
	{
		private const string USS_CLASS = "wm-fullmenu";
		private const string ACTIVE_CLASS = "wm-fullmenu--active";

		/// <summary> UIRoot 가 만든 직후 넣어 줌 </summary>
		public FullMenuSettings Settings { get; set; }
		public MobileControlIcons Icons { get; set; }

		public bool IsOpen { get; private set; }

		private VisualElement container;
		private VisualElement safeContent;
		private VisualElement stage;
		private FullMenuCoreStage coreStage;
		// 설정창과 같은 UIRoot 오브젝트를 정지 열쇠로 쓰면 서로의 정지를 풀어 버림. 메뉴는 자기 오브젝트를 열쇠로
		private GameObject pauseKey;
		private bool returnFromSetting;
		private VisualElement focusOnReturn;
		private readonly List<(InputEventType EventType, Action Handler)> hotkeyHandlers = new();

		private UIRoot uiRoot;
		private InputManager inputManager;
		private DataManager dataManager;
		private TimeManager timeManager;
		private WorldClock worldClock;
		private WindowManager windowManager;

		[Inject]
		public void Construct(InputManager inputManager, DataManager dataManager, TimeManager timeManager, WorldClock worldClock, WindowManager windowManager)
		{
			this.inputManager = inputManager;
			this.dataManager = dataManager;
			this.timeManager = timeManager;
			this.worldClock = worldClock;
			this.windowManager = windowManager;
		}

		private void Awake()
		{
			uiRoot = GetComponent<UIRoot>();
		}

		private void Start()
		{
			if (Settings == null)
				throw new InvalidOperationException($"{nameof(FullMenuView)}: UIRoot 프리팹의 fullMenuSettings 가 비어 있음");

			// 어두운 배경은 화면 끝까지 (container), 버튼과 글은 안전 영역 (safeContent)
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, "FullMenu");
			container = frame.Root;
			safeContent = frame.Content;
			container.AddToClassList(USS_CLASS);
			container.styleSheets.Add(Settings.Style);

			pauseKey = new GameObject("FullMenuCoreStage");
			pauseKey.transform.SetParent(transform, false);

			BuildUI();
			BuildNavigation();
			RegisterHotkeys();

			uiRoot.SettingView.Closed += OnSettingClosed;
			IsOpen = false;
		}

		private void OnDestroy()
		{
			// 열린 채 씬이 바뀌면 정지와 흐림 요청이 남음
			if (IsOpen)
			{
				SetBlurred(false);
				if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
					existingTimeManager.Resume(pauseKey);
			}

			if (InputManager.TryGetExistingInstance(out InputManager existingInputManager))
			{
				foreach ((InputEventType eventType, Action handler) in hotkeyHandlers)
					existingInputManager.UnregisterInputEvent(eventType, InputEventResponseType.Performed, handler);
			}

			if (uiRoot != null && uiRoot.SettingView != null)
				uiRoot.SettingView.Closed -= OnSettingClosed;
			container?.RemoveFromHierarchy();
			if (glowTexture != null)
				Destroy(glowTexture);
		}

		public void Open()
		{
			if (IsOpen)
				return;
			IsOpen = true;
			returnFromSetting = false;

			// 메뉴의 창 칸은 단축키를 누른 것과 같아서, 이미 열린 창이 있으면 누를 때 닫힘. 열기 전에 창을 정리
			while (windowManager.GetTopmostOpen() != null)
				windowManager.GetTopmostOpen().Close();
			if (uiRoot.MagicBookView.IsOpen)
				uiRoot.MagicBookView.Close();

			EnsureCoreStage();
			RefreshEntries();
			RefreshInfo();
			coreStage.SetActive(true);
			SetBlurred(UIEffects.Enabled);
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(container, ACTIVE_CLASS);
			timeManager.Pause(pauseKey);

			// 손가락 조작에는 선택 칸이 없음. 누르지도 않은 칸이 강조돼 보이지 않게
			if (inputManager.IsTouchMode == false)
				FocusWhenReady();
		}

		public void Close()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;

			// 닫힌 뒤 확인 키가 숨은 버튼을 누르지 않게 포커스를 놓음
			if (container.focusController?.focusedElement is VisualElement focused && container.Contains(focused))
				focused.Blur();

			UiSfx.Play(UiSfxEvent.WindowClose);
			UIMotion.Hide(container, ACTIVE_CLASS);
			coreStage.SetActive(false);
			SetBlurred(false);
			timeManager.Resume(pauseKey);
		}

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}

		// 코어 무대 (카메라, RenderTexture, 모델) 는 처음 열 때. 메뉴를 열지 않는 시작 화면에서는 만들지 않음
		private void EnsureCoreStage()
		{
			if (coreStage != null)
				return;
			coreStage = pauseKey.AddComponent<FullMenuCoreStage>();
			coreStage.Build(Settings);
			coreImage.image = coreStage.Texture;
			coreImage.EnableInClassList(HIDDEN_CLASS, coreStage.HasModel == false);
		}

		// 메뉴가 열린 동안 창 단축키를 누르면 그 창은 자기 처리로 열림. 메뉴는 닫혀서 창이 메뉴 뒤에 깔리지 않게
		private void RegisterHotkeys()
		{
			foreach (Entry entry in entries)
			{
				if (entry.Hotkey.HasValue == false)
					continue;
				Action handler = CloseForHotkey;
				inputManager.RegisterInputEvent(entry.Hotkey.Value, InputEventResponseType.Performed, handler);
				hotkeyHandlers.Add((entry.Hotkey.Value, handler));
			}
		}

		private void CloseForHotkey()
		{
			if (IsOpen)
				Close();
		}

		// 메뉴를 닫고 그 창의 단축키를 누른 것과 같게. 창마다 여는 길을 따로 두지 않음
		private void OpenWindow(InputEventType inputEventType)
		{
			Close();
			inputManager.PressFromScreenButton(inputEventType);
			inputManager.ReleaseFromScreenButton(inputEventType);
		}

		private void OpenSetting()
		{
			LeaveForSetting();
			uiRoot.SettingView.Open();
		}

		private void AskQuit()
		{
			LeaveForSetting();
			uiRoot.SettingView.OpenQuitConfirm();
		}

		private void LeaveForSetting()
		{
			focusOnReturn = container.focusController?.focusedElement as VisualElement;
			Close();
			returnFromSetting = true;
		}

		private void OnSettingClosed(bool byUser)
		{
			bool shouldReturn = returnFromSetting && byUser;
			returnFromSetting = false;
			if (shouldReturn)
				Open();
			else
				focusOnReturn = null;
		}

		private void RefreshInfo()
		{
			nyangLabel.text = dataManager.GameStat[GameStatType.NYANG].ToString("N0") + "냥";

			bool hasStage = StageManager.TryGetExistingInstance(out StageManager stageManager) && stageManager.CurStage != null;
			placeLabel.text = hasStage ? stageManager.CurStage.DisplayName : string.Empty;

			string weather = WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem)
				? ", " + WeatherDisplayName.Of(weatherSystem.Current)
				: string.Empty;
			timeLabel.text = $"{worldClock.Day}일차 {worldClock.Hour:D2}:{worldClock.Minute:D2}{weather}";

			Doll doll = SOHelper.GetDoll(dataManager.CurDollID);
			dollNameLabel.text = doll.Name;
			dollLevelLabel.text = "Lv. " + doll.Level;
			dollPortrait.style.backgroundImage = doll.Sprite != null ? new StyleBackground(doll.Sprite) : StyleKeyword.None;
		}

		[ContextMenu("Open Full Menu")]
		private void DebugOpen() => Open();

		[ContextMenu("Close Full Menu")]
		private void DebugClose() => Close();
	}
}
