using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Idle.UI;
using WitchMendokusai.Presentation;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// V2 작전 화면. 정본 <c>memo/wm/design/idle/layout.md</c> (사용자 확정 2026-08-30).
	///
	/// ★ 화면은 둘. 왼쪽 <b>전투 창</b>(1200)과 오른쪽 <b>관리 열</b>(720).
	///   HUD 는 전부 전투 창 안: 작전 코드, 웨이브, 스테퍼, 반복, 골드, 손패, 코스트, AUTO.
	///   관리 열은 탭 7 + 판 하나. 한 번에 한 판.
	/// ★ 분할 토글: 전투 창과 관리 열을 chevron 하나로 접고 펼침.
	/// ★ 상점, 연구소 탭은 전투 창의 3D 씬 자리를 그 탭의 씬으로 바꾼다 (지금은 자리 표시만).
	/// ★ 규칙은 한 줄도 없다. 사진을 그리고 의도를 보낸다. 판정은 전부 코어.
	/// ★ 설정: 배속과 전투 기록. 골드 상세: HUD 골드 아이콘.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(PanelRenderer))]
	public sealed class BattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치 자산")]
		[SerializeField] private TuningSO tuningAsset;
		[SerializeField] private HeroCatalogSO heroCatalogAsset;
		[SerializeField] private UIContentSO uiContentAsset;
		[SerializeField] private GearPresentationSO gearPresentationAsset;
		[SerializeField] private RuntimeSettingsSO runtimeSettingsAsset;

		[Header("UI Builder 정본과 반복 템플릿")]
		[SerializeField] private IdleViewAssetsSO viewAssets;

		[Header("무대. 씬이 꽂아 준다")]
		[SerializeField] private BattleStage stage;

		private VisualTreeAsset screenAsset => viewAssets.Screen;
		private VisualTreeAsset cardAsset => viewAssets.Card;
		private VisualTreeAsset queueChipAsset => viewAssets.QueueChip;
		private VisualTreeAsset choiceCardAsset => viewAssets.ChoiceCard;
		private VisualTreeAsset waveDotAsset => viewAssets.WaveDot;
		private VisualTreeAsset rowButtonAsset => viewAssets.RowButton;

		private IdleSession session;
		private float untilUiRefresh;
		private SessionPersistence persistence;
		private ProceduralSfx sound;
		private bool clickSoundHooked;
		private ScreenRootController screenRootController;
		private VisualElement panelRoot;

		// 에디트 모드 미리보기 (사용자 2026-08-30: UI 수정은 Play 없이). 저장 읽기와 쓰기 없음. 임시 판 위 시뮬만
		/// <summary>화면 에셋이 없어 못 짓는 판. 켜 두되 아무것도 안 그린다</summary>
		private bool broken;

		private bool preview;

#if UNITY_EDITOR
		// 미리보기 시계와 첫 틱 표식. 에디터 전용 경로에서만 읽으므로 여기 밖에 두면
		// 플레이어 빌드에서 CS0414(쓰기만 하고 안 읽음)로 죽는다 (실측 2026-09-01, csc.rsp 가 -warnaserror+)
		private double previewClock;
		private bool previewTicked;
#endif

		/// <summary>미리보기 시뮬 진행 여부. 기본은 첫 틱 뒤 정지 (정적 장면). Dev Panel 이 켠다</summary>
		public static bool PreviewRunning { get; set; }

		// 짓기가 끝나야 그린다. 짓는 도중 Render 가 돌면 아직 없는 조각(맵 팝업)에서 죽는다 (실측 2026-08-30)
		private bool built;

		// ── 탭 ────────────────────────────────────────────────────────────
		// ── 전투 창 ───────────────────────────────────────────────────────
		private VisualElement battle;
		private BattleHudController battleHudController;
		private BattleActionController battleActionController;

		private CardHandController cardHandController;

		// ── 관리 열 ───────────────────────────────────────────────────────
		/// <summary>UI 뿌리. 폭을 재서 무대 카메라를 맞춘다</summary>
		private VisualElement root;

		private SidePanelController sidePanelController;
		private ScreenLayoutController screenLayoutController;
		private SidePagesController sidePagesController;
		private ManagementPage openPage = ManagementPage.Doll;

		private SelectionPopupCoordinator selectionPopupCoordinator;

		// 툴팁
		private Label tooltip;
		private PointerTooltipController tooltipController;

		// 팝업
		private AuxiliaryPopupCoordinator auxiliaryPopupCoordinator;
		private ModalController modalController;
		private HeroVisualPresenter heroVisualPresenter;
		private GearVisualPresenter gearVisualPresenter;

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			// UXML 이 정본 (사용자 2026-08-30). 없으면 조용한 빈 화면 대신 여기서 정지
			//
			// ⚠ enabled 를 끄면 그 상태가 씬에 저장됨 (실측 2026-08-31). 에셋을 채워도 복구 불가
			//   플래그만 세우고 컴포넌트는 켠 채로
			if (MissingAsset(out string what))
			{
				Debug.LogError("[Idle] 화면 에셋이 없다: " + what + ". Dev Panel 의 씬 짓기로 다시 꽂아라");
				broken = true;
				return;
			}

			broken = false;
			IdleHeroes.Configure(heroCatalogAsset.ToDomain());
			heroVisualPresenter = new HeroVisualPresenter(heroCatalogAsset);
			gearVisualPresenter = new GearVisualPresenter(gearPresentationAsset);

			// 배치 빌드에서는 아무것도 안 세운다 (실측 2026-09-01: 20회 연속 빌드 실패).
			// 씬 검사(IdleSceneBuilder.Verify)가 씬을 열면 [ExecuteAlways] 때문에 여기가 돌고,
			// -nographics 배치에는 카메라도 패널도 없음. 빌드가 Unknown 으로 사망
			if (Application.isBatchMode)
			{
				return;
			}

			screenRootController = new ScreenRootController(
				GetComponent<PanelRenderer>(),
				screenAsset,
				OnPanelReloaded);
			screenRootController.Enable();

			IdleTuning tuning = tuningAsset.ToTuning();
			preview = Application.isPlaying == false;
			persistence = null;

			IdleState state;
			IdleAwayReport away = default;

			if (preview)
			{
#if UNITY_EDITOR
				previewTicked = false;
#endif
				state = runtimeSettingsAsset.CreatePreviewState(tuning);
				session = new IdleSession(tuning, state);
#if UNITY_EDITOR
				UnityEditor.EditorApplication.update -= PreviewTick;
				UnityEditor.EditorApplication.update += PreviewTick;
				previewClock = UnityEditor.EditorApplication.timeSinceStartup;
#endif
			}
			else
			{
				persistence = new SessionPersistence(runtimeSettingsAsset.SaveIntervalSeconds);
				state = persistence.LoadState();
				session = new IdleSession(tuning, state);
				away = persistence.CatchUp(session);
				EnsureSound();
			}

			if (stage != null)
			{
				stage.Build();
			}
			else
			{
				Debug.LogWarning("[Idle] 무대가 안 꽂혀 있다. HUD 만 뜬다. 씬 빌더로 다시 지어라.");
			}

			BuildAll(away);
			Render(session.Capture());
		}

#if UNITY_EDITOR
		/// <summary>에디트 모드의 한 틱. 시뮬을 밟고 모든 뷰를 다시 그린다</summary>
		private void PreviewTick()
		{
			if (this == null || preview == false)
			{
				UnityEditor.EditorApplication.update -= PreviewTick;
				return;
			}

			double now = UnityEditor.EditorApplication.timeSinceStartup;
			float delta = Mathf.Min(0.25f, (float)(now - previewClock));
			previewClock = now;

			// 정적 장면이 기본. 첫 틱만 밟아 전장을 세우고 멈춘다 (사용자: UI 와 정적 3D 확인용)
			if (previewTicked && PreviewRunning == false)
			{
				return;
			}

			previewTicked = true;
			Tick(delta);
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}
#endif

		/// <summary>안 꽂힌 화면 에셋 이름. 전부 있으면 거짓</summary>
		private bool MissingAsset(out string what)
		{
			what = string.Empty;

			if (tuningAsset == null) { what = "tuningAsset"; }
			else if (heroCatalogAsset == null) { what = "heroCatalogAsset"; }
			else if (heroCatalogAsset.TryValidate(out string heroError) == false)
			{
				what = "heroCatalogAsset: " + heroError;
			}
			else if (uiContentAsset == null) { what = "uiContentAsset"; }
			else if (gearPresentationAsset == null) { what = "gearPresentationAsset"; }
			else if (runtimeSettingsAsset == null) { what = "runtimeSettingsAsset"; }
			else if (viewAssets == null) { what = "viewAssets"; }
			else if (viewAssets.TryValidate(out string viewError) == false)
			{
				what = "viewAssets: " + viewError;
			}
			else if (uiContentAsset.TryValidate(
				System.Enum.GetValues(typeof(ManagementPage)).Length, out string uiError) == false)
			{
				what = "uiContentAsset: " + uiError;
			}
			else if (gearPresentationAsset.TryValidate(out string gearError) == false)
			{
				what = "gearPresentationAsset: " + gearError;
			}
			else if (runtimeSettingsAsset.TryValidate(out string runtimeError) == false)
			{
				what = "runtimeSettingsAsset: " + runtimeError;
			}

			return what.Length > 0;
		}

		private void OnDisable()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.update -= PreviewTick;
#endif
			screenRootController?.Dispose();
			screenRootController = null;
			panelRoot = null;
			clickSoundHooked = false;
			modalController?.Dispose();
			if (preview)
			{
				session = null;
				return;
			}

			persistence?.Close(session);
			session = null;
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				WriteDown();
			}
		}

		private void OnApplicationQuit()
		{
			WriteDown();
		}

		private void Update()
		{
			// 미리보기는 에디터 틱(PreviewTick) 담당. 에디트 모드의 플레이어 루프는 Update 를 안 부름 (실측 2026-08-30)
			if (preview)
			{
				return;
			}

			if (built == false)
			{
				BuildAll(default);
				if (built == false)
				{
					return;
				}
			}

			Tick(Time.unscaledDeltaTime);
		}

		private void Tick(float delta)
		{
			if (session == null || broken)
			{
				return;
			}

			// 보고 있는 동안은 위험 진행. 적의 공격, 쓰러짐, 부활
			session.AdvanceLive(delta);
			session.AdvanceSurge(delta);
			IdleSnapshot snapshot = session.Capture();

			if (stage != null)
			{
				stage.Render(snapshot, delta);
			}

			auxiliaryPopupCoordinator?.Tick(delta);

			untilUiRefresh -= delta;
			if (untilUiRefresh <= 0f)
			{
				untilUiRefresh = runtimeSettingsAsset.UIRefreshSeconds;
				Render(snapshot);
			}

			if (preview)
			{
				return;
			}

			persistence?.Tick(delta, session);
		}

		private void WriteDown()
		{
			persistence?.Save(session);
		}

		// ── 짓기 ──────────────────────────────────────────────────────────

		private void BuildAll(IdleAwayReport away)
		{
			// PanelRenderer OnEnable 전 호출 방어. 이전 판 완료 상태로 빈 라벨에 닿지 않게
			built = false;
			modalController?.Dispose();
			ResetViewCollections();
			if (panelRoot == null)
			{
				return;
			}

			this.root = panelRoot;
			VisualElement root = this.root;
			modalController = new ModalController(root, runtimeSettingsAsset.ModalRepaintMilliseconds);
			if (clickSoundHooked == false)
			{
				root.RegisterCallback<ClickEvent>(OnButtonClicked);
				clickSoundHooked = true;
			}

			// 창 크기가 바뀌면 무대 폭도 다시 (모바일 회전, PC 창 조절)
			VisualElement shell = root.Q<VisualElement>("shell");
			tooltip = root.Q<Label>("tooltip");
			tooltipController = new PointerTooltipController(tooltip, runtimeSettingsAsset.TooltipTouchMilliseconds);
			battleActionController = new BattleActionController(
				session,
				stage,
				uiContentAsset,
				runtimeSettingsAsset,
				() => cardHandController.CancelAim(),
				() => auxiliaryPopupCoordinator.CloseMap(),
				WriteDown,
				() => Render(session.Capture()),
				SayOnce);

			BuildBattle(shell);
			BuildSide(shell);
			screenLayoutController = new ScreenLayoutController(
				root, sidePanelController, battleHudController, uiContentAsset);
			BuildAuxiliaryPopups();
			BuildSelectionPopups();
			auxiliaryPopupCoordinator.ShowAway(UsePopup("away-popup-host"), away);

			if (stage != null)
			{
				stage.SetFloatingTextRoot(battle);
			}

			built = true;
			screenLayoutController.Apply((int)openPage);

		}

		private void OnPanelReloaded(VisualElement rootElement)
		{
			if (panelRoot != rootElement)
			{
				clickSoundHooked = false;
			}

			panelRoot = rootElement;
			if (session == null || broken)
			{
				return;
			}

			BuildAll(default);
			Render(session.Capture());
		}

		private void ResetViewCollections()
		{
			screenLayoutController?.Dispose();
			battleHudController = null;
			battleActionController = null;
			cardHandController = null;
			sidePanelController = null;
			screenLayoutController = null;
			sidePagesController = null;
			selectionPopupCoordinator = null;
			auxiliaryPopupCoordinator = null;
		}

		private void BuildBattle(VisualElement shell)
		{
			battle = shell.Q<VisualElement>("battle");

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(battleActionController.OnBattleTapped);

			BindBattleHud();
			BuildBattleExtras();
		}

		private void BuildBattleExtras()
		{
			Button wipe = battle.Q<Button>("wipe-button");
			wipe.style.display = Application.isEditor || Debug.isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
			wipe.clicked += WipeAndRestart;
			cardHandController.BringAimToFront();
			wipe.BringToFront();
		}

		private void BindBattleHud()
		{
			battleHudController = new BattleHudController(
				battle,
				waveDotAsset,
				uiContentAsset,
				session.CanGoToStage,
				() => OpenPage(ManagementPage.Doll),
				() => auxiliaryPopupCoordinator.ToggleMap(),
				battleActionController.StepStage,
				battleActionController.ToggleHold,
				() => auxiliaryPopupCoordinator.OpenGold(),
				ToggleSplit,
				() => auxiliaryPopupCoordinator.OpenSettings(),
				battleActionController.ToggleAutoCast);
			cardHandController = new CardHandController(
				battle,
				cardAsset,
				queueChipAsset,
				uiContentAsset,
				battleActionController.CanAimCard,
				battleActionController.Cast,
				battleActionController.PickFoe,
				battleActionController.CastVolleyAt);
		}

		private void BuildSide(VisualElement shell)
		{
			sidePanelController = new SidePanelController(
				shell, uiContentAsset,
				index => OpenPage((ManagementPage)index));
			sidePagesController = new SidePagesController(
				sidePanelController,
				root,
				session,
				uiContentAsset,
				viewAssets,
				heroVisualPresenter,
				gearVisualPresenter,
				() => selectionPopupCoordinator.HeroId,
				() => selectionPopupCoordinator.GearSeat,
				() => selectionPopupCoordinator.SelectingPartySeat,
				slot => selectionPopupCoordinator.OpenHero(slot),
				slot => selectionPopupCoordinator.OpenGear(slot),
				HookTooltip,
				WriteDown,
				() => Render(session.Capture()),
				SayOnce,
				() => sound?.Good(),
				runtimeSettingsAsset.NoteSeconds);
		}

		private VisualElement UsePopup(string hostName)
		{
			VisualElement host = root.Q<VisualElement>(hostName);
			VisualElement popup = host.Q<VisualElement>("popup");
			popup.style.display = DisplayStyle.None;
			return popup;
		}

		private void BuildAuxiliaryPopups()
		{
			auxiliaryPopupCoordinator = new AuxiliaryPopupCoordinator(
				UsePopup("map-popup-host"),
				UsePopup("gold-popup-host"),
				UsePopup("settings-popup-host"),
				rowButtonAsset,
				modalController,
				session,
				uiContentAsset,
				() => selectionPopupCoordinator.CloseAll(),
				battleActionController.GoToStage,
				() => Render(session.Capture()));
		}

		/// <summary>장비 고르기 팝업. 관리 열 위에 뜬다</summary>
		private void BuildSelectionPopups()
		{
			selectionPopupCoordinator = new SelectionPopupCoordinator(
				UsePopup("hero-popup-host"),
				UsePopup("gear-popup-host"),
				choiceCardAsset,
				modalController,
				heroVisualPresenter,
				gearVisualPresenter,
				session,
				uiContentAsset,
				sidePagesController.ItemPage,
				auxiliaryPopupCoordinator.CloseGoldAndSettings,
				WriteDown,
				() => Render(session.Capture()),
				SayOnce,
				runtimeSettingsAsset.NoteSeconds);
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (built == false)
			{
				return;
			}

			battleHudController.Render(snapshot);
			auxiliaryPopupCoordinator.Render(snapshot);

			RenderHand(snapshot);
			RenderTabBadges(snapshot);

			if (screenLayoutController.ContentVisible)
			{
				sidePagesController.Render(openPage, snapshot);
				selectionPopupCoordinator.Render(snapshot);
			}

		}

		private void RenderHand(IdleSnapshot snapshot)
		{
			cardHandController.Render(snapshot);
		}

		private void RenderTabBadges(IdleSnapshot snapshot)
		{
			sidePanelController.RenderBadges(
				snapshot, (int)openPage, screenLayoutController.ContentVisible);
		}

		// ── 화면 상태 ─────────────────────────────────────────────────────

		private void OpenPage(ManagementPage page)
		{
			openPage = page;
			selectionPopupCoordinator.ClearHeroSelection();

			// 상점, 연구소는 왼쪽 씬이 바뀐다 (layout.md §2). 지금은 덮개
			bool altScene = page == ManagementPage.Shop || page == ManagementPage.Lab;
			battleHudController.SetAlternateScene(altScene,
				uiContentAsset.ScenePlaceholderText(page == ManagementPage.Shop));

			screenLayoutController.OpenSide((int)openPage);
			Render(session.Capture());
		}

		private void ToggleSplit()
		{
			screenLayoutController.ToggleSplit((int)openPage);
			Render(session.Capture());
		}

		/// <summary>
		/// 저장 삭제 뒤 처음부터 재시작. 디버그 전용
		///
		/// ★ 끄면서 저장하는 길(<see cref="OnDisable"/>)이 지운 것을 되살리지 않게 차단 뒤 끔
		/// </summary>
		private void WipeAndRestart()
		{
			// 미리보기는 저장과 무관. 임시 판만 새로
			if (preview)
			{
				enabled = false;
				enabled = true;
				return;
			}

			persistence.WipeAndSkipClose();
			enabled = false;
			enabled = true;
		}

		// ── 툴팁 ───────────────────────────────────────────────────────────

		/// <summary>마우스를 올리면 뜨는 설명. PC 우선 (layout.md §1). 글은 부르는 쪽이 만든다</summary>
		/// <summary>
		/// 설명 붙이기. 마우스는 올리면, 손가락은 누르면
		///
		/// ★ 모바일에 호버 없음 (2026-09-01). 호버만 걸면 장비 정보 조회 불가
		/// </summary>
		private void HookTooltip(VisualElement target, System.Func<string> text)
		{
			tooltipController.Bind(target, text);
		}

		private void EnsureSound()
		{
			if (sound == null && Application.isPlaying && Application.isBatchMode == false)
			{
				sound = new ProceduralSfx(gameObject, runtimeSettingsAsset.SoundVolume, runtimeSettingsAsset.SoundMinGapSeconds);
			}
		}

		private void OnButtonClicked(ClickEvent moment)
		{
			if (moment.target is Button button && button.ClassListContains("idle-stat-buy") == false)
			{
				sound?.Click();
			}
		}

		// ── 잔손 ──────────────────────────────────────────────────────────

		private void SayOnce(string what, float seconds)
		{
			auxiliaryPopupCoordinator.ShowNote(what, seconds);
		}

	}
}
