using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체화면 환경설정 UI (엔드필드/오버워치 스타일).
	/// UIRoot.ScreenLayer에 직접 VisualElement를 추가함.
	/// 좌측 사이드바의 탭 버튼 클릭 → 우측 컨텐츠 swap (data-driven, RegisterTab).
	/// </summary>
	public class SettingView : MonoBehaviour
	{
		private const string USS_CLASS = "wm-setting-view";
		private const string ACTIVE_CLASS = "wm-setting-view--active";
		private const string TAB_KEY_GENERAL = "general";
		private const string TAB_KEY_SHADERPACKS = "shaderpacks";

		private VisualElement container;
		private VisualElement sidebar;
		private VisualElement contentArea;

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

			// Frosted glass — RT 직접 reference (USS URL 의존 X). CustomBlurFeature 가 매 frame 그림 (Settings open 동안).
			RenderTexture blurOutput = Resources.Load<RenderTexture>("Rendering/CustomBlurOutput");
			if (blurOutput != null)
			{
				container.style.backgroundImage = Background.FromRenderTexture(blurOutput);
			}

			BuildUI();

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
			sidebar = new VisualElement();
			sidebar.AddToClassList("wm-setting-sidebar");

			Label titleLabel = new Label("환경설정");
			titleLabel.AddToClassList("wm-setting-title");
			sidebar.Add(titleLabel);

			contentArea = new VisualElement();
			contentArea.AddToClassList("wm-setting-content-area");

			RegisterTab(TAB_KEY_GENERAL, "환경설정", BuildGeneralContent);
			RegisterTab(TAB_KEY_SHADERPACKS, "쉐이더팩", BuildShaderPackContent);

			SwitchTab(TAB_KEY_GENERAL);

			container.Add(sidebar);
			container.Add(contentArea);
		}

		protected void RegisterTab(string tabKey, string title, Func<VisualElement> contentBuilder)
		{
			Button tabButton = new Button(() => SwitchTab(tabKey)) { text = title };
			tabButton.AddToClassList("wm-setting-tab");
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
			content.Add(masterVolume);

			bgmVolume = CreateSlider("배경음악 (BGM)", 0f, 1f, audioManager.GetVolume(AudioManager.BusType.BGM));
			bgmVolume.RegisterValueChangedCallback(evt => audioManager.SetVolume(AudioManager.BusType.BGM, evt.newValue));
			content.Add(bgmVolume);

			sfxVolume = CreateSlider("효과음 (SFX)", 0f, 1f, audioManager.GetVolume(AudioManager.BusType.SFX));
			sfxVolume.RegisterValueChangedCallback(evt => audioManager.SetVolume(AudioManager.BusType.SFX, evt.newValue));
			content.Add(sfxVolume);

			// System
			Label systemHeader = new Label("시스템");
			systemHeader.AddToClassList("wm-setting-header");
			content.Add(systemHeader);

			framerateToggle = new Toggle("60 FPS 고정");
			framerateToggle.AddToClassList("wm-setting-toggle");
			framerateToggle.value = Application.targetFrameRate == 60;
			framerateToggle.RegisterValueChangedCallback(evt => Application.targetFrameRate = evt.newValue ? 60 : 30);
			content.Add(framerateToggle);

			// 버튼 그룹
			VisualElement buttonGroup = new VisualElement();
			buttonGroup.AddToClassList("wm-setting-buttons");

			btnDungeonExit = new Button(OnDungeonExit) { text = "던전 포기" };
			btnDungeonExit.AddToClassList("wm-setting-btn");
			btnDungeonExit.AddToClassList("wm-setting-btn--danger");
			buttonGroup.Add(btnDungeonExit);

			Button btnSaveInit = new Button(OnClearData) { text = "세이브 데이터 초기화" };
			btnSaveInit.AddToClassList("wm-setting-btn");
			btnSaveInit.AddToClassList("wm-setting-btn--danger");
			buttonGroup.Add(btnSaveInit);

			Button btnQuit = new Button(OnQuit) { text = "게임 종료" };
			btnQuit.AddToClassList("wm-setting-btn");
			buttonGroup.Add(btnQuit);

			content.Add(buttonGroup);

			Button btnClose = new Button(Close) { text = "닫기 (ESC)" };
			btnClose.AddToClassList("wm-setting-close");
			content.Add(btnClose);

			return content;
		}

		private VisualElement BuildShaderPackContent()
		{
			VisualElement root = new VisualElement();

			// 헤더 + 액션 버튼 (폴더 열기 / 재스캔)
			VisualElement header = new VisualElement();
			header.AddToClassList("wm-setting-shaderpack-header");

			Label title = new Label("쉐이더팩");
			title.AddToClassList("wm-setting-header");
			header.Add(title);

			Button btnOpenFolder = new Button(OnOpenShaderPacksFolder) { text = "폴더 열기" };
			btnOpenFolder.AddToClassList("wm-setting-btn");
			header.Add(btnOpenFolder);

			Button btnRescan = new Button(OnRescanShaderPacks) { text = "재스캔" };
			btnRescan.AddToClassList("wm-setting-btn");
			header.Add(btnRescan);

			root.Add(header);

			// Split: 좌측 list / 우측 detail
			VisualElement split = new VisualElement();
			split.AddToClassList("wm-setting-shaderpack-split");

			shaderPackListContainer = new VisualElement();
			shaderPackListContainer.AddToClassList("wm-setting-shaderpack-list");
			split.Add(shaderPackListContainer);

			shaderPackDetailContainer = new VisualElement();
			shaderPackDetailContainer.AddToClassList("wm-setting-shaderpack-detail");
			split.Add(shaderPackDetailContainer);

			root.Add(split);

			RebuildShaderPackList();
			RebuildShaderPackDetail();

			return root;
		}

		private void RebuildShaderPackList()
		{
			if (shaderPackListContainer == null)
				return;

			shaderPackListContainer.Clear();

			IReadOnlyList<ShaderPackEntry> packs = shaderPackManager.AvailablePacks;

			if (packs.Count == 0)
			{
				Label emptyLabel = new Label("(셰이더팩 없음 — '폴더 열기' 로 셰이더팩 추가)");
				emptyLabel.AddToClassList("wm-setting-shaderpack-empty");
				shaderPackListContainer.Add(emptyLabel);
				return;
			}

			ShaderPackEntry activePack = shaderPackManager.ActivePack;

			foreach (ShaderPackEntry pack in packs)
			{
				ShaderPackEntry capturedPack = pack;
				Button packButton = new Button(() => SelectShaderPack(capturedPack)) { text = pack.Manifest.name };
				packButton.AddToClassList("wm-setting-shaderpack-item");
				if (activePack == pack)
					packButton.AddToClassList("wm-setting-shaderpack-item--active");
				if (selectedShaderPack == pack)
					packButton.AddToClassList("wm-setting-shaderpack-item--selected");
				shaderPackListContainer.Add(packButton);
			}
		}

		private void SelectShaderPack(ShaderPackEntry pack)
		{
			selectedShaderPack = pack;
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private void RebuildShaderPackDetail()
		{
			if (shaderPackDetailContainer == null)
				return;

			shaderPackDetailContainer.Clear();

			if (selectedShaderPack == null)
			{
				Label hint = new Label("좌측에서 셰이더팩을 선택하세요");
				hint.AddToClassList("wm-setting-shaderpack-hint");
				shaderPackDetailContainer.Add(hint);
				return;
			}

			ShaderPackManifest manifest = selectedShaderPack.Manifest;

			Label nameLabel = new Label(manifest.name);
			nameLabel.AddToClassList("wm-setting-shaderpack-detail-name");
			shaderPackDetailContainer.Add(nameLabel);

			Label authorLabel = new Label($"by {manifest.author}");
			authorLabel.AddToClassList("wm-setting-shaderpack-detail-author");
			shaderPackDetailContainer.Add(authorLabel);

			Label versionLabel = new Label($"v{manifest.version}");
			versionLabel.AddToClassList("wm-setting-shaderpack-detail-version");
			shaderPackDetailContainer.Add(versionLabel);

			if (string.IsNullOrEmpty(manifest.description) == false)
			{
				Label descLabel = new Label(manifest.description);
				descLabel.AddToClassList("wm-setting-shaderpack-detail-desc");
				shaderPackDetailContainer.Add(descLabel);
			}

			bool isActive = shaderPackManager.ActivePack == selectedShaderPack;

			if (isActive)
			{
				Button btnRevert = new Button(OnRevertShaderPack) { text = "언로드" };
				btnRevert.AddToClassList("wm-setting-btn");
				shaderPackDetailContainer.Add(btnRevert);
			}
			else
			{
				Button btnApply = new Button(OnApplyShaderPack) { text = "적용" };
				btnApply.AddToClassList("wm-setting-btn");
				shaderPackDetailContainer.Add(btnApply);
			}
		}

		private void OnOpenShaderPacksFolder()
		{
			string folder = shaderPackManager.ShaderPacksDirectory;
			if (System.IO.Directory.Exists(folder) == false)
				System.IO.Directory.CreateDirectory(folder);
			Application.OpenURL("file://" + folder);
		}

		private void OnRescanShaderPacks()
		{
			shaderPackManager.ScanShaderPacks();
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private void OnApplyShaderPack()
		{
			shaderPackManager.Apply(selectedShaderPack.Id);
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private void OnRevertShaderPack()
		{
			shaderPackManager.Revert();
			RebuildShaderPackList();
			RebuildShaderPackDetail();
		}

		private static Slider CreateSlider(string label, float min, float max, float value)
		{
			Slider slider = new Slider(label, min, max)
			{
				value = value
			};
			slider.AddToClassList("wm-setting-slider");
			return slider;
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
			container.AddToClassList(ACTIVE_CLASS);

			// HACK: World 씬에서만 던전 포기 버튼 활성화
			bool isWorld = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "World";
			bool isDungeon = DungeonManager.TryGetExistingInstance(out DungeonManager dm) && dm.IsDungeon;
			btnDungeonExit.style.display = (isWorld && isDungeon) ? DisplayStyle.Flex : DisplayStyle.None;

			// 쉐이더팩 탭 진입 시 최신 상태 반영 (다른 곳에서 ShaderPackManager 변경했을 수 있음)
			RebuildShaderPackList();
			RebuildShaderPackDetail();

			// Frosted glass — blur pass capture 동안 panel repaint 매 frame (RT update 따라가기).
			// paused 후엔 씬 변화 0 이라 100ms 면 충분 → 그 후 BlurRequest.Remove + repaint 종료.
			BlurRequest.Add();
			IVisualElementScheduledItem repaintItem = container.schedule.Execute(() => container.MarkDirtyRepaint()).Every(16);
			container.schedule.Execute(() =>
			{
				BlurRequest.Remove();
				repaintItem.Pause();
			}).ExecuteLater(100);

			timeManager.Pause(gameObject);
		}

		public void Close()
		{
			if (IsOpen == false) return;
			IsOpen = false;
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
