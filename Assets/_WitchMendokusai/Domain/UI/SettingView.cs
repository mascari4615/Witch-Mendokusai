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

		// 데모 목표 144FPS (사용자 2026-11-12). 토글 끔: 품질 단계의 수직 동기만
		[SerializeField, Tooltip("FPS 고정 토글 켬")] private int lockedFrameRate = 60;
		[SerializeField, Tooltip("FPS 고정 토글 끔. -1 제한 없음")] private int unlockedFrameRate = -1;

		[SerializeField] private float sliderStep = 0.05f;
		[SerializeField] private float navOverlapTolerance = 4f;
		[SerializeField] private float navAcrossWeight = 2f;
		[SerializeField] private int volumeDisplayScale = 100;
		[SerializeField] private float frameOutset = 4f;

		private VisualElement container;
		private VisualElement stage;
		private VisualElement sidebar;
		private VisualElement contentArea;
		private Button closeButton;

		private readonly Dictionary<string, VisualElement> tabContents = new();
		private readonly Dictionary<string, Button> tabButtons = new();
		private string currentTabKey;

		private Button btnDungeonExit;

		// Audio
		private Slider masterVolume;
		private Slider bgmVolume;
		private Slider sfxVolume;

		// System
		private Toggle framerateToggle;

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

		[Inject]
		public void Construct(AudioManager audioManager, ShaderPackManager shaderPackManager, DataManager dataManager, TimeManager timeManager)
		{
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
			container = new VisualElement();
			container.AddToClassList(USS_CLASS);
			uiRoot.ScreenLayer.Add(container);

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

			container?.RemoveFromHierarchy();
		}

		private void OnPlayerObjectBound(PlayerObjectBoundEvent evt) => playerObject = evt.Object;
		private void OnPlayerDespawned(PlayerDespawnedEvent evt) => playerObject = null;

		private void BuildUI()
		{
			stage = new VisualElement();
			stage.AddToClassList("wm-setting-stage");
			container.Add(stage);
			container.RegisterCallback<GeometryChangedEvent>(_ => FitStage());
			stage.RegisterCallback<GeometryChangedEvent>(_ => FitStage());

			Label titleLabel = new Label("환경설정");
			titleLabel.AddToClassList("wm-setting-title");
			stage.Add(titleLabel);

			closeButton = new Button(Close) { text = "닫기 (ESC)" };
			closeButton.AddToClassList("wm-setting-close");
			closeButton.AddToClassList(NAV_CLASS);
			stage.Add(closeButton);

			VisualElement panel = new VisualElement();
			panel.AddToClassList("wm-setting-panel");
			stage.Add(panel);
			VisualElement panelLine = new VisualElement { pickingMode = PickingMode.Ignore };
			panelLine.AddToClassList("wm-setting-panel__line");
			panel.Add(panelLine);
			VisualElement panelCorner = new VisualElement { pickingMode = PickingMode.Ignore };
			panelCorner.AddToClassList("wm-setting-panel__corner");
			panel.Add(panelCorner);

			sidebar = new VisualElement();
			sidebar.AddToClassList("wm-setting-tabs");
			panel.Add(sidebar);

			contentArea = new VisualElement();
			contentArea.AddToClassList("wm-setting-content-area");
			panel.Add(contentArea);

			RegisterTab(TAB_KEY_GENERAL, "환경설정", BuildGeneralContent);
			RegisterTab(TAB_KEY_EFFECTS, "효과", BuildEffectsContent);
			RegisterTab(TAB_KEY_SHADERPACKS, "쉐이더팩", BuildShaderPackContent);
			RegisterTab(TAB_KEY_CREDITS, "크레딧", BuildCreditsContent);

			SwitchTab(TAB_KEY_GENERAL);
		}

		// USS의 기준 화면을 가로와 세로에 함께 맞추고 남은 공간 중앙에 배치
		private void FitStage()
		{
			float width = stage.resolvedStyle.width;
			float height = stage.resolvedStyle.height;
			float scale = Mathf.Min(container.resolvedStyle.width / width, container.resolvedStyle.height / height);
			stage.style.scale = new Scale(new Vector3(scale, scale, 1f));
			stage.style.left = (container.resolvedStyle.width - width * scale) * .5f;
			stage.style.top = (container.resolvedStyle.height - height * scale) * .5f;
		}

		protected void RegisterTab(string tabKey, string title, Func<VisualElement> contentBuilder)
		{
			Button tabButton = new Button(() => SwitchTab(tabKey)) { text = title };
			tabButton.AddToClassList("wm-setting-tab");
			tabButton.AddToClassList(NAV_CLASS);
			sidebar.Add(tabButton);
			tabButtons[tabKey] = tabButton;

			VisualElement content = contentBuilder();
			content.AddToClassList("wm-setting-content");
			content.style.display = DisplayStyle.None;
			contentArea.Add(content);
			tabContents[tabKey] = content;
		}

		private void SwitchTab(string tabKey)
		{
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

			// System
			Label systemHeader = new Label("시스템");
			systemHeader.AddToClassList("wm-setting-header");
			content.Add(systemHeader);

			framerateToggle = CreateToggle("60 FPS 고정", Application.targetFrameRate == lockedFrameRate, isOn => isOn ? "켜짐 (60)" : "꺼짐 (제한 없음)", isOn => Application.targetFrameRate = isOn ? lockedFrameRate : unlockedFrameRate, content);

			// 버튼 그룹
			VisualElement buttonGroup = new VisualElement();
			buttonGroup.AddToClassList("wm-setting-buttons");

			btnDungeonExit = new Button(() => AskConfirmWithSound("던전을 포기할까요?", "이번 판은 패배로 끝납니다.", "포기", OnDungeonExit)) { text = "던전 포기" };
			btnDungeonExit.AddToClassList("wm-setting-btn");
			btnDungeonExit.AddToClassList("wm-setting-btn--danger");
			btnDungeonExit.AddToClassList(NAV_CLASS);
			buttonGroup.Add(btnDungeonExit);

			Button btnSaveInit = new Button(() => AskConfirmWithSound("세이브 데이터를 초기화할까요?", "되돌릴 수 없습니다.", "초기화", OnClearData)) { text = "세이브 데이터 초기화" };
			btnSaveInit.AddToClassList("wm-setting-btn");
			btnSaveInit.AddToClassList("wm-setting-btn--danger");
			btnSaveInit.AddToClassList(NAV_CLASS);
			buttonGroup.Add(btnSaveInit);

			Button btnQuit = new Button(() => AskConfirmWithSound("게임을 종료할까요?", "진행 중인 내용은 저장 시점까지만 남습니다.", "종료", OnQuit)) { text = "게임 종료" };
			btnQuit.AddToClassList("wm-setting-btn");
			btnQuit.AddToClassList(NAV_CLASS);
			buttonGroup.Add(btnQuit);

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

			Label uiHeader = new Label("화면 효과");
			uiHeader.AddToClassList("wm-setting-header");
			content.Add(uiHeader);

			CreateOnOffToggle("UI 연출", UIEffects.Enabled, isOn => UIEffects.Enabled = isOn, content);

			return content;
		}

		private void CreateOnOffToggle(string label, bool value, Action<bool> onChanged, VisualElement parent)
			=> CreateToggle(label, value, isOn => isOn ? "켜짐" : "꺼짐", onChanged, parent);

		private Toggle CreateToggle(string label, bool value, Func<bool, string> describe, Action<bool> onChanged, VisualElement parent)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("wm-setting-row");

			Toggle toggle = new Toggle(label) { value = value };
			toggle.AddToClassList("wm-setting-toggle");
			toggle.AddToClassList(NAV_CLASS);
			row.Add(toggle);

			Label state = new Label(describe(value));
			state.AddToClassList("wm-setting-toggle-state");
			row.Add(state);

			toggle.RegisterValueChangedCallback(evt =>
			{
				UiSfx.Play(UiSfxEvent.Confirm);
				onChanged(evt.newValue);
				state.text = describe(evt.newValue);
			});

			parent.Add(row);
			return toggle;
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
			slider.RegisterCallback<GeometryChangedEvent>(AddSliderTile);
			return slider;
		}

		// fill 요소는 첫 배치 때 생성
		private static void AddSliderTile(GeometryChangedEvent evt)
		{
			Slider slider = (Slider)evt.target;
			VisualElement fillElement = UQueryExtensions.Q(slider, null, "unity-base-slider__fill");
			if (fillElement == null)
				return;
			slider.UnregisterCallback<GeometryChangedEvent>(AddSliderTile);
			VisualElement tile = new VisualElement { pickingMode = PickingMode.Ignore };
			tile.AddToClassList("wm-setting-slider__tile");
			fillElement.Add(tile);
		}

		private void OnDungeonExit()
		{
			Close();
			if (playerObject == null)
				return;
			playerObject.ReceiveDamage(new DamageInfo
			{
				damage = 9999,
				type = DamageType.Critical,
				damageSource = playerObject,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
				ignoreInvincible = true,
			});
		}

		private void OnClearData() => dataManager.CreateNewGameData();

		private void OnQuit() => Application.Quit();

		public void Open()
		{
			if (IsOpen) return;
			IsOpen = true;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			container.AddToClassList(ACTIVE_CLASS);

			// HACK: World 씬에서만 던전 포기 버튼 활성화
			bool isWorld = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "World";
			bool isDungeon = DungeonManager.TryGetExistingInstance(out DungeonManager dm) && dm.IsDungeon;
			btnDungeonExit.style.display = (isWorld && isDungeon) ? DisplayStyle.Flex : DisplayStyle.None;

			// 쉐이더팩 탭 진입 시 최신 상태 반영 (다른 곳에서 ShaderPackManager 변경했을 수 있음)
			RebuildShaderPackList();
			RebuildShaderPackDetail();

			masterVolume.schedule.Execute(() =>
			{
				masterVolume.Focus();
				MoveFrameTo(masterVolume);
			});

			timeManager.Pause(gameObject);
		}

		public void Close()
		{
			if (IsOpen == false) return;
			if (IsConfirmOpen)
			{
				CancelConfirm();
				return;
			}
			IsOpen = false;
			UiSfx.Play(UiSfxEvent.WindowClose);
			selectFrame.AddToClassList(HIDDEN_FRAME_CLASS);
			container.RemoveFromClassList(ACTIVE_CLASS);
			timeManager.Resume(gameObject);
		}

		public void Toggle()
		{
			if (IsOpen) Close();
			else Open();
		}
	}
}
