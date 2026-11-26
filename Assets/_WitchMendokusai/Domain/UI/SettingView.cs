using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체화면 환경설정 UI (전체 화면 목록형).
	/// UIRoot.ScreenLayer에 직접 VisualElement를 추가함.
	/// 위 탭 → 아래 컨텐츠 swap (data-driven, RegisterTab).
	/// </summary>
	public partial class SettingView : MonoBehaviour
	{
		private const string USS_CLASS = "wm-setting-view";
		private const string ACTIVE_CLASS = "wm-setting-view--active";
		private const string TAB_KEY_GENERAL = "general";
		private const string TAB_KEY_EFFECTS = "effects";
		private const string TAB_KEY_SHADERPACKS = "shaderpacks";
		private const string CLEAR_DATA_TEXT = "세이브 데이터 초기화";
		private const string CLEAR_DATA_BLOCKED_TEXT = "세이브 데이터 초기화 (시작 화면에서만)";
		private const string TOGGLE_ON_CLASS = "wm-setting-toggle-state--on";

		[SerializeField] private float sliderStep = 0.05f;
		[SerializeField] private float navOverlapTolerance = 4f;
		[SerializeField] private float navAcrossWeight = 2f;
		[SerializeField] private int volumeDisplayScale = 100;
		[SerializeField] private float frameOutset = 4f;

		private VisualElement container;
		private VisualElement safeContent;
		private VisualElement stage;
		private VisualElement tabBar;
		private VisualElement sideList;
		private VisualElement contentArea;
		private Button closeButton;

		private readonly Dictionary<string, VisualElement> tabContents = new();
		private readonly Dictionary<string, Button> tabButtons = new();
		private readonly List<string> tabOrder = new();
		private string currentTabKey;

		// Audio
		private Slider masterVolume;
		private Slider bgmVolume;
		private Slider sfxVolume;
		private Button clearDataButton;

		// Display
		private Toggle fpsDisplayToggle;

		// Shaderpacks
		private VisualElement shaderPackListContainer;
		private VisualElement shaderPackDetailContainer;
		private ShaderPackEntry selectedShaderPack;

		public bool IsOpen { get; private set; }

		private UnitObject playerObject;

		private UIRoot uiRoot;
		private AudioManager audioManager;
		private ShaderPackManager shaderPackManager;
		private DataManager dataManager;
		private TimeManager timeManager;
		private InputManager inputManager;

		[Inject]
		public void Construct(AudioManager audioManager, ShaderPackManager shaderPackManager, DataManager dataManager, TimeManager timeManager, InputManager inputManager)
		{
			this.inputManager = inputManager;
			this.audioManager = audioManager;
			this.shaderPackManager = shaderPackManager;
			this.dataManager = dataManager;
			this.timeManager = timeManager;
		}

		private void Awake()
		{
			uiRoot = GetComponent<UIRoot>();
			EventBusBridge.Subscribe<PlayerObjectBoundEvent>(OnPlayerObjectBound);
			EventBusBridge.Subscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
		}

		private void Start()
		{
			// 어두운 막은 화면 끝까지 (container), 설정판은 안전 영역 (safeContent) 가운데
			FullScreenFrame frame = uiRoot.CreateFullScreenFrame(uiRoot.ScreenLayer, "SettingView");
			container = frame.Root;
			safeContent = frame.Content;
			container.AddToClassList(USS_CLASS);

			BuildNavigation();
			BuildUI();
			BuildConfirm();

			// 초기 상태: 닫힘 (display: none 으로 처리됨)
			IsOpen = false;
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<PlayerObjectBoundEvent>(OnPlayerObjectBound);
			EventBusBridge.Unsubscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
			KeybindRegistry.OnChanged -= RefreshKeyRows;
			inputManager?.CancelRebind();

			container?.RemoveFromHierarchy();
		}

		private void OnPlayerObjectBound(PlayerObjectBoundEvent evt) => playerObject = evt.Object;
		private void OnPlayerDespawned(PlayerDespawnedEvent evt) => playerObject = null;

		private void BuildUI()
		{
			stage = new VisualElement();
			stage.AddToClassList("wm-setting-stage");
			safeContent.Add(stage);
			safeContent.RegisterCallback<GeometryChangedEvent>(_ => FitStage());
			stage.RegisterCallback<GeometryChangedEvent>(_ => FitStage());

			Label titleLabel = new Label("// 설정");
			titleLabel.AddToClassList("wm-setting-title");
			stage.Add(titleLabel);

			closeButton = KeyHint.KeyButton("닫기", "ESC", Close);
			closeButton.AddToClassList("wm-setting-close");
			closeButton.AddToClassList(NAV_CLASS);
			stage.Add(closeButton);

			// 위 가운데 아이콘 탭, 양끝에 탭 넘김 키 (Q, E). 폰과 손가락 조작에서는 키 칸을 숨김
			tabBar = new VisualElement();
			tabBar.AddToClassList("wm-setting-tabs");
			stage.Add(tabBar);
			tabBar.Add(CreateTabKeyCap("Q"));

			// 판은 틀 없이 자리만. 왼쪽 묶음 목록과 본문이 나란히
			VisualElement panel = new VisualElement();
			panel.AddToClassList("wm-setting-panel");
			stage.Add(panel);

			sideList = new VisualElement();
			sideList.AddToClassList("wm-setting-side");
			panel.Add(sideList);

			contentArea = new VisualElement();
			contentArea.AddToClassList("wm-setting-content-area");
			panel.Add(contentArea);

			RegisterTab(TAB_KEY_GENERAL, "환경설정", BuildGeneralContent, true);
			RegisterTab(TAB_KEY_DISPLAY, "화면", BuildDisplayContent, true);
			RegisterTab(TAB_KEY_KEYS, "키", BuildKeysContent);
			RegisterTab(TAB_KEY_EFFECTS, "효과", BuildEffectsContent, true);
			RegisterTab(TAB_KEY_SHADERPACKS, "쉐이더팩", BuildShaderPackContent);
			RegisterTab(TAB_KEY_CREDITS, "크레딧", BuildCreditsContent);
			tabBar.Add(CreateTabKeyCap("E"));

			Label footer = KeyHint.KeyOnly(new Label("Q E 탭 넘기기"));
			footer.AddToClassList("wm-setting-footer");
			stage.Add(footer);

			// 키 탭은 키보드 조작에서만. 폰과 손가락 조작은 화면 버튼이라 바꿀 키가 없음
			if (Application.isMobilePlatform || inputManager.IsTouchMode)
				tabButtons[TAB_KEY_KEYS].style.display = DisplayStyle.None;

			container.RegisterCallback<KeyDownEvent>(OnTabKeyDown, TrickleDown.TrickleDown);
			SwitchTab(TAB_KEY_GENERAL);
		}

		private static Label CreateTabKeyCap(string key)
		{
			Label keyCap = KeyHint.KeyOnly(new Label(key));
			keyCap.AddToClassList("wm-setting-tab-key");
			return keyCap;
		}

		// Q 와 E 로 탭을 넘김 (끝에서 돌아감). 탭 버튼으로 초점을 옮기면 OnFocusIn 이 탭을 바꿈
		private void OnTabKeyDown(KeyDownEvent evt)
		{
			int step = evt.keyCode == KeyCode.E ? 1 : evt.keyCode == KeyCode.Q ? -1 : 0;
			// 키를 기다리는 중과 확인 창에서는 글자 키가 다른 뜻이라 건드리지 않음
			if (step == 0 || IsOpen == false || IsConfirmOpen || inputManager.IsRebinding)
				return;

			int count = tabOrder.Count;
			int index = tabOrder.IndexOf(currentTabKey);
			for (int offset = 1; offset <= count; offset++)
			{
				string tabKey = tabOrder[((index + step * offset) % count + count) % count];
				// 폰의 키 탭처럼 숨긴 탭은 건너뜀
				if (tabButtons[tabKey].style.display == DisplayStyle.None)
					continue;
				tabButtons[tabKey].Focus();
				evt.StopPropagation();
				return;
			}
		}

		// USS의 기준 화면을 가로와 세로에 함께 맞추고 남은 공간 중앙에 배치
		private void FitStage()
		{
			float width = stage.resolvedStyle.width;
			float height = stage.resolvedStyle.height;
			float scale = Mathf.Min(safeContent.resolvedStyle.width / width, safeContent.resolvedStyle.height / height);
			stage.style.scale = new Scale(new Vector3(scale, scale, 1f));
			stage.style.left = (safeContent.resolvedStyle.width - width * scale) * .5f;
			stage.style.top = (safeContent.resolvedStyle.height - height * scale) * .5f;
		}

		// 탭은 아이콘 (USS 의 wm-setting-tab--키), 이름은 툴팁. hasSections: 머리글마다 묶음을 가르고 왼쪽 목록에 올림
		protected void RegisterTab(string tabKey, string title, Func<VisualElement> contentBuilder, bool hasSections = false)
		{
			Button tabButton = new Button(() => SwitchTab(tabKey)) { tooltip = title };
			tabButton.AddToClassList("wm-setting-tab");
			tabButton.AddToClassList("wm-setting-tab--" + tabKey);
			tabButton.AddToClassList(NAV_CLASS);
			tabBar.Add(tabButton);
			tabButtons[tabKey] = tabButton;
			tabOrder.Add(tabKey);

			VisualElement content = contentBuilder();
			content.AddToClassList("wm-setting-content");
			content.style.display = DisplayStyle.None;
			contentArea.Add(content);
			tabContents[tabKey] = content;
			if (hasSections)
				BuildSections(tabKey, content);
		}

		private void SwitchTab(string tabKey)
		{
			// 키를 기다리는 중 탭을 옮기면 기다림을 끝냄
			inputManager.CancelRebind();
			if (currentTabKey == tabKey)
				return;

			if (currentTabKey != null)
			{
				tabContents[currentTabKey].style.display = DisplayStyle.None;
				tabButtons[currentTabKey].RemoveFromClassList("wm-setting-tab--active");
				UiSfx.Play(UiSfxEvent.Tab);
			}

			tabContents[tabKey].style.display = DisplayStyle.Flex;
			tabButtons[tabKey].AddToClassList("wm-setting-tab--active");
			currentTabKey = tabKey;
			ShowSideGroup(tabKey);
		}

		private VisualElement BuildGeneralContent()
		{
			VisualElement content = new VisualElement();

			// Audio
			Label audioHeader = new Label("오디오");
			audioHeader.AddToClassList("wm-setting-header");
			content.Add(audioHeader);

			masterVolume = CreateSlider("마스터 볼륨", 0f, 1f, audioManager.GetVolume(AudioManager.BusType.Master));
			masterVolume.RegisterValueChangedCallback(evt => audioManager.SetVolume(AudioManager.BusType.Master, evt.newValue));
			content.Add(WrapSlider(masterVolume));

			bgmVolume = CreateSlider("배경음악 (BGM)", 0f, 1f, audioManager.GetVolume(AudioManager.BusType.BGM));
			bgmVolume.RegisterValueChangedCallback(evt => audioManager.SetVolume(AudioManager.BusType.BGM, evt.newValue));
			content.Add(WrapSlider(bgmVolume));

			sfxVolume = CreateSlider("효과음 (SFX)", 0f, 1f, audioManager.GetVolume(AudioManager.BusType.SFX));
			sfxVolume.RegisterValueChangedCallback(evt => audioManager.SetVolume(AudioManager.BusType.SFX, evt.newValue));
			content.Add(WrapSlider(sfxVolume));

			// 위험한 동작은 따로 묶음 (왼쪽 목록의 데이터 칸)
			Label dataHeader = new Label("데이터");
			dataHeader.AddToClassList("wm-setting-header");
			content.Add(dataHeader);

			VisualElement buttonGroup = new VisualElement();
			buttonGroup.AddToClassList("wm-setting-buttons");

			// 끼임 탈출, 던전 포기, 로비로, 게임 종료는 전체 메뉴에 (설정이 아니라 게임을 다루는 동작). 시작 화면에는 자기 종료 버튼이 있음
			clearDataButton = new Button(() => AskConfirmWithSound("세이브 데이터를 초기화할까요?", "되돌릴 수 없습니다.", "초기화", OnClearData)) { text = CLEAR_DATA_TEXT };
			clearDataButton.AddToClassList("wm-setting-btn");
			clearDataButton.AddToClassList("wm-setting-btn--danger");
			clearDataButton.AddToClassList(NAV_CLASS);
			buttonGroup.Add(clearDataButton);

			content.Add(buttonGroup);

			return content;
		}

		private VisualElement BuildEffectsContent()
		{
			VisualElement content = new VisualElement();

			Label combatHeader = new Label("전투 효과");
			combatHeader.AddToClassList("wm-setting-header");
			content.Add(combatHeader);

			CreateOnOffToggle("피해 숫자", CombatEffectSettings.DamageNumbers, isOn => CombatEffectSettings.DamageNumbers = isOn, content);
			CreateOnOffToggle("피격 번쩍임", CombatEffectSettings.Flash, isOn => CombatEffectSettings.Flash = isOn, content);
			CreateOnOffToggle("화면 흔들림", CombatEffectSettings.ScreenShake, isOn => CombatEffectSettings.ScreenShake = isOn, content);
			content.Add(CreateRemainsLingerSlider());

			Label uiHeader = new Label("화면 효과");
			uiHeader.AddToClassList("wm-setting-header");
			content.Add(uiHeader);

			CreateOnOffToggle("UI 연출", UIEffects.Enabled, isOn => UIEffects.Enabled = isOn, content);

			return content;
		}

		private void CreateOnOffToggle(string label, bool value, Action<bool> onChanged, VisualElement parent)
			=> CreateToggle(label, value, DescribeOnOff, onChanged, parent);

		private static string DescribeOnOff(bool isOn) => isOn ? "켜짐" : "꺼짐";

		private Toggle CreateToggle(string label, bool value, Func<bool, string> describe, Action<bool> onChanged, VisualElement parent)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("wm-setting-row");

			Toggle toggle = new Toggle(label) { value = value };
			toggle.AddToClassList("wm-setting-toggle");
			toggle.AddToClassList(NAV_CLASS);
			row.Add(toggle);

			// 켬과 끔은 알약 하나. 알약을 눌러도 바뀜
			Label state = new Label(describe(value));
			state.AddToClassList("wm-setting-toggle-state");
			state.EnableInClassList(TOGGLE_ON_CLASS, value);
			state.RegisterCallback<ClickEvent>(_ => toggle.value = toggle.value == false);
			row.Add(state);

			toggle.RegisterValueChangedCallback(evt =>
			{
				UiSfx.Play(UiSfxEvent.Confirm);
				onChanged(evt.newValue);
				state.text = describe(evt.newValue);
				state.EnableInClassList(TOGGLE_ON_CLASS, evt.newValue);
			});

			parent.Add(row);
			return toggle;
		}

		// 값만 바꾸고 소리와 콜백 없이 상태 글자도 맞춤
		private static void SyncToggle(Toggle toggle, bool value, Func<bool, string> describe)
		{
			if (toggle.value == value)
				return;
			toggle.SetValueWithoutNotify(value);
			Label state = toggle.parent.Q<Label>(className: "wm-setting-toggle-state");
			state.text = describe(value);
			state.EnableInClassList(TOGGLE_ON_CLASS, value);
		}

		private VisualElement WrapSlider(Slider slider)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("wm-setting-row");
			row.Add(slider);

			Label valueLabel = new Label(FormatVolume(slider.value));
			valueLabel.AddToClassList("wm-setting-value");
			row.Add(valueLabel);

			slider.RegisterValueChangedCallback(evt =>
			{
				valueLabel.text = FormatVolume(evt.newValue);
				UiSfx.Play(UiSfxEvent.Slider);
			});
			return row;
		}

		// 싸움 흔적 유지 시간. 선택지 번호를 칸 단위로 고르는 막대
		private VisualElement CreateRemainsLingerSlider()
		{
			RemainsSettings remains = RemainsSettings.Load();
			return CreateStepSlider("싸움 흔적 유지", remains.LingerChoices.Length, CombatEffectSettings.RemainsLingerChoice, choice => FormatLinger(remains, choice),
				choice => CombatEffectSettings.RemainsLingerChoice = choice, out Slider _, out Label _);
		}

		private static string FormatLinger(RemainsSettings remains, int choice)
		{
			float seconds = remains.LingerSecondsAt(choice);
			if (float.IsPositiveInfinity(seconds))
				return "판 내내";
			return seconds <= 0f ? "끔" : $"{Mathf.RoundToInt(seconds)}초";
		}

		private string FormatVolume(float value) => Mathf.RoundToInt(value * volumeDisplayScale).ToString();

		private static Slider CreateSlider(string label, float min, float max, float value)
		{
			Slider slider = new Slider(label, min, max)
			{
				value = value
			};
			slider.AddToClassList("wm-setting-slider");
			slider.AddToClassList(NAV_CLASS);
			slider.fill = true;
			return slider;
		}

		// 확인 창이 뜬 사이 씬이 바뀔 수 있어 실행할 때 다시 봄
		private void OnClearData()
		{
			SyncClearData();
			if (CanClearData == false)
			{
				Debug.LogWarning($"[{nameof(SettingView)}] 시작 화면이 아니라 세이브 초기화 생략");
				return;
			}
			dataManager.CreateNewGameData();
		}

		// 로비에서, 데이터를 다 읽은 뒤에만. 월드의 화면과 판은 옛 데이터를 들고 있어 거기서 지우면 던전 퀘스트가 빠짐
		private bool CanClearData => LobbyManager.Instance != null && dataManager.IsDataLoaded;

		private void SyncClearData()
		{
			clearDataButton.SetEnabled(CanClearData);
			clearDataButton.text = CanClearData ? CLEAR_DATA_TEXT : CLEAR_DATA_BLOCKED_TEXT;
		}

		public void Open()
		{
			if (IsOpen) return;
			IsOpen = true;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			UIMotion.Show(container, ACTIVE_CLASS);

			// 쉐이더팩 탭 진입 시 최신 상태 반영 (다른 곳에서 ShaderPackManager 변경했을 수 있음)
			RebuildShaderPackList();
			RebuildShaderPackDetail();

			// 개발자 창 표시 탭에서 바뀌었을 수 있음
			SyncToggle(fpsDisplayToggle, UIFPS.IsVisible, DescribeOnOff);
			SyncDisplayRows();
			SyncClearData();

			masterVolume.schedule.Execute(() =>
			{
				masterVolume.Focus();
				MoveFrameTo(masterVolume);
			});

			timeManager.Pause(gameObject);
		}

		public void Close() => Close(true);

		// byUser: 사용자가 설정창을 닫은 것. 던전 포기처럼 게임으로 바로 돌아가는 동작은 false
		private void Close(bool byUser)
		{
			if (IsOpen == false) return;
			// 키를 기다리는 중에 닫히면 기다림을 끝냄 (입력 잠금이 남지 않게)
			inputManager.CancelRebind();
			if (IsConfirmOpen)
			{
				CancelConfirm();
				return;
			}
			IsOpen = false;
			// 닫힌 뒤 확인 키가 숨은 버튼을 누르지 않게 포커스를 놓음 (FullMenuView.Close 와 같음)
			if (container.focusController?.focusedElement is VisualElement focused && container.Contains(focused))
				focused.Blur();
			UiSfx.Play(UiSfxEvent.WindowClose);
			selectFrame.AddToClassList(HIDDEN_FRAME_CLASS);
			UIMotion.Hide(container, ACTIVE_CLASS);
			timeManager.Resume(gameObject);
			Closed(byUser);
		}

		/// <summary> 설정창이 닫힌 직후. 인자는 사용자가 닫았는지. 전체 메뉴가 받아 그때만 다시 열림 </summary>
		public event Action<bool> Closed = delegate { };

		public void Toggle()
		{
			if (IsOpen) Close();
			else Open();
		}
	}
}

