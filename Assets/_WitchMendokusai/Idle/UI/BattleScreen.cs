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
	///   HUD 는 전부 전투 창 안: 작전 코드, 웨이브, 스테퍼, 반복, 재화 3, 배속, 로그, 손패, 코스트, AUTO.
	///   관리 열은 탭 7 + 판 하나. 한 번에 한 판.
	/// ★ 분할 토글: 전투 풀화면이면 관리 열이 오른쪽에서 덮는 서랍, 탭은 우하.
	/// ★ 상점, 연구소 탭은 전투 창의 3D 씬 자리를 그 탭의 씬으로 바꾼다 (지금은 자리 표시만).
	/// ★ 규칙은 한 줄도 없다. 사진을 그리고 의도를 보낸다. 판정은 전부 코어.
	/// ★ 이름(골드, 뽑기, 환생 조각, 탭 이름)은 전부 임시 (layout.md §6).
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(PanelRenderer))]
	public sealed class BattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치. 비워 두면 코드 기본값")]
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
		private VisualTreeAsset bagCellAsset => viewAssets.BagCell;
		private VisualTreeAsset forgeKindAsset => viewAssets.ForgeKind;
		private VisualTreeAsset cardAsset => viewAssets.Card;
		private VisualTreeAsset queueChipAsset => viewAssets.QueueChip;
		private VisualTreeAsset choiceCardAsset => viewAssets.ChoiceCard;
		private VisualTreeAsset waveDotAsset => viewAssets.WaveDot;
		private VisualTreeAsset producerRowAsset => viewAssets.ProducerRow;
		private VisualTreeAsset rowButtonAsset => viewAssets.RowButton;
		private VisualTreeAsset rowLabelAsset => viewAssets.RowLabel;

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
		private enum Tab { Doll = 0, Item = 1, Codex = 2, Shop = 3, Lab = 4, Dungeon = 5, Invest = 6 }

		// ── 전투 창 ───────────────────────────────────────────────────────
		private VisualElement battle;
		private BattleHudController battleHudController;

		private CardHandController cardHandController;
		private DungeonPageController dungeonPageController;

		// ── 관리 열 ───────────────────────────────────────────────────────
		/// <summary>UI 뿌리. 폭을 재서 무대 카메라를 맞춘다</summary>
		private VisualElement root;

		private SidePanelController sidePanelController;
		private ScreenLayoutController screenLayoutController;
		private Tab openTab = Tab.Doll;

		private DollPageController dollPageController;
		private HeroSelectionController heroSelectionController;

		/// <summary>장비를 볼 인형의 편성 자리 (2026-08-31 인형별 장비). 찬 편성 칸을 누르면 바뀐다</summary>
		private int gearSeat;

		/// <summary>그 자리의 인형 번호. 빈 자리면 -1</summary>
		private int gearHeroId => session != null ? session.HeroAtPartySlot(gearSeat) : -1;

		// 장비 고르기 팝업 (사용자 2026-08-31). 인형이 여럿이라 가방에서 바로 장착하면 대상이 불명
		private GearSelectionController gearSelectionController;

		private ItemPageController itemPageController;

		private CodexPageController codexPageController;
		private ShopPageController shopPageController;
		private LabPageController labPageController;
		private InvestPageController investPageController;

		// 툴팁
		private Label tooltip;
		private PointerTooltipController tooltipController;

		// 팝업
		private MapSelectionController mapSelectionController;
		private GoldDetailsController goldDetailsController;
		private SettingsPopupController settingsPopupController;
		private ModalController modalController;
		private HeroVisualPresenter heroVisualPresenter;
		private GearVisualPresenter gearVisualPresenter;

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			if (tuningAsset == null)
			{
				Debug.LogWarning("[Idle] 수치 에셋이 안 꽂혀 있다. 코드 기본값으로 돈다.");
			}

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

			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();
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

			if (heroCatalogAsset == null) { what = "heroCatalogAsset"; }
			else if (uiContentAsset == null) { what = "uiContentAsset"; }
			else if (gearPresentationAsset == null) { what = "gearPresentationAsset"; }
			else if (runtimeSettingsAsset == null) { what = "runtimeSettingsAsset"; }
			else if (viewAssets == null) { what = "viewAssets"; }
			else if (viewAssets.TryValidate(out string viewError) == false)
			{
				what = "viewAssets: " + viewError;
			}
			else if (uiContentAsset.TryValidate(System.Enum.GetValues(typeof(Tab)).Length, out string uiError) == false)
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

			settingsPopupController?.Tick(delta);

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

			BuildBattle(shell);
			BuildSide(shell);
			screenLayoutController = new ScreenLayoutController(
				root, sidePanelController, battleHudController, uiContentAsset);
			BuildMapPopup();
			BuildGearPopup();
			BuildHeroPopup();
			BuildGoldPopup();
			BuildSettingsPopup();
			BuildAwayPopup(away);

			if (stage != null)
			{
				stage.SetFloatingTextRoot(battle);
			}

			built = true;
			screenLayoutController.Apply((int)openTab);

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
			cardHandController = null;
			sidePanelController = null;
			screenLayoutController = null;
			dollPageController = null;
			heroSelectionController = null;
			gearSelectionController = null;
			itemPageController = null;
			codexPageController = null;
			shopPageController = null;
			labPageController = null;
			dungeonPageController = null;
			investPageController = null;
			mapSelectionController = null;
			goldDetailsController = null;
			settingsPopupController = null;
		}

		private void BuildBattle(VisualElement shell)
		{
			battle = shell.Q<VisualElement>("battle");

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(OnTapped);

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
				() => OpenTab(Tab.Doll),
				ToggleMap,
				StepStage,
				ToggleHold,
				OpenGoldPopup,
				ToggleSplit,
				OpenSettingsPopup,
				ToggleAutoCast);
			cardHandController = new CardHandController(
				battle,
				cardAsset,
				queueChipAsset,
				uiContentAsset,
				CanAimCard,
				Cast,
				PickFoe,
				CastVolleyAt);
		}

		private bool CanAimCard(int handIndex)
		{
			if (session == null || handIndex < 0)
			{
				return false;
			}

			IdleSnapshot snapshot = session.Capture();
			return handIndex < snapshot.Cards.Length
				&& snapshot.Cards[handIndex].Kind == IdleCardKind.Volley
				&& snapshot.Cards[handIndex].CanCast;
		}

		private long? PickFoe(Vector2 position)
		{
			return stage != null && stage.TryPickFoe(position, out long foeIndex) ? foeIndex : (long?)null;
		}

		private bool CastVolleyAt(int handIndex, long foeIndex)
		{
			if (session.TryCastCardAt(handIndex, foeIndex, out IdleCardResult result) == false)
			{
				return false;
			}
			stage.OnVolley(foeIndex);
			SayOnce(uiContentAsset.VolleyTargetFeedback, runtimeSettingsAsset.NoteSeconds);
			WriteDown();
			Render(session.Capture());
			return true;
		}

		private void BuildSide(VisualElement shell)
		{
			sidePanelController = new SidePanelController(
				shell, battle, uiContentAsset, index => OpenTab((Tab)index), CloseSide);
			BuildDollPage();
			BuildItemPage();
			BuildCodexPage();
			BuildShopPage();
			BuildLabPage();
			BuildDungeonPage();
			BuildInvestPage();
		}

		private VisualElement UsePage(Tab tab, string hostName)
		{
			return sidePanelController.BindPage((int)tab, hostName, root);
		}

		private VisualElement UsePopup(string hostName)
		{
			VisualElement host = root.Q<VisualElement>(hostName);
			VisualElement popup = host.Q<VisualElement>("popup");
			popup.style.display = DisplayStyle.None;
			return popup;
		}

		/// <summary>인형 탭 (layout.md §3). 모양은 UXML, 여기는 값과 클릭만</summary>
		private void BuildDollPage()
		{
			dollPageController = new DollPageController(
				UsePage(Tab.Doll, "doll-page-host"),
				session,
				uiContentAsset,
				heroVisualPresenter,
				gearVisualPresenter,
				() => gearHeroId,
				() => gearSeat,
				() => heroSelectionController != null ? heroSelectionController.SelectedSeat : -1,
				OpenHeroPopup,
				OpenGear,
				WornTip,
				HookTooltip,
				WriteDown,
				() => Render(session.Capture()),
				() => sound?.Good());
		}

		/// <summary>아이템 탭 (layout.md §3). 가방과 공방. 모양은 UXML</summary>
		private void BuildItemPage()
		{
			itemPageController = new ItemPageController(
				UsePage(Tab.Item, "item-page-host"),
				session,
				uiContentAsset,
				gearVisualPresenter,
				bagCellAsset,
				forgeKindAsset,
				rowButtonAsset,
				() => gearHeroId,
				WriteDown,
				() => Render(session.Capture()),
				SayOnce,
				HookTooltip,
				runtimeSettingsAsset.NoteSeconds);
		}

		private void BuildCodexPage()
		{
			codexPageController = new CodexPageController(
				UsePage(Tab.Codex, "codex-page-host"), rowLabelAsset, uiContentAsset);
		}

		private void BuildShopPage()
		{
			shopPageController = new ShopPageController(
				UsePage(Tab.Shop, "shop-page-host"), session, uiContentAsset,
				WriteDown, () => Render(session.Capture()), SayOnce, runtimeSettingsAsset.NoteSeconds);
		}

		private void BuildLabPage()
		{
			labPageController = new LabPageController(
				UsePage(Tab.Lab, "lab-page-host"), session, uiContentAsset,
				WriteDown, () => Render(session.Capture()), SayOnce, runtimeSettingsAsset.NoteSeconds);
		}

		/// <summary>던전 넷 (economy.md). 알파 9번이라 지금은 눌리지 않는다</summary>
		private void BuildDungeonPage()
		{
			dungeonPageController = new DungeonPageController(
				UsePage(Tab.Dungeon, "dungeon-page-host"), uiContentAsset);
		}

		private void BuildInvestPage()
		{
			investPageController = new InvestPageController(
				UsePage(Tab.Invest, "invest-page-host"), producerRowAsset,
				session, uiContentAsset, () => Render(session.Capture()));
		}

		private void BuildMapPopup()
		{
			mapSelectionController = new MapSelectionController(
				UsePopup("map-popup-host"),
				rowButtonAsset,
				modalController,
				uiContentAsset,
				session.CanGoToStage,
				GoToStage);
		}

		/// <summary>장비 고르기 팝업. 관리 열 위에 뜬다</summary>
		private void BuildGearPopup()
		{
			gearSelectionController = new GearSelectionController(
				UsePopup("gear-popup-host"),
				choiceCardAsset,
				modalController,
				gearVisualPresenter,
				uiContentAsset,
				Equip);
		}

		private void BuildHeroPopup()
		{
			heroSelectionController = new HeroSelectionController(
				UsePopup("hero-popup-host"),
				choiceCardAsset,
				modalController,
				heroVisualPresenter,
				uiContentAsset,
				ChooseHero);
		}

		private void BuildGoldPopup()
		{
			goldDetailsController = new GoldDetailsController(
				UsePopup("gold-popup-host"), modalController, uiContentAsset);
		}

		private void BuildSettingsPopup()
		{
			settingsPopupController = new SettingsPopupController(
				UsePopup("settings-popup-host"), modalController,
				session, uiContentAsset, () => Render(session.Capture()));
		}

		private void OpenGoldPopup()
		{
			goldDetailsController.Open(() =>
			{
				CloseMap();
				CloseHeroPopup();
				CloseGear();
				CloseSettingsPopup();
			});
			Render(session.Capture());
		}

		private void CloseGoldPopup()
		{
			goldDetailsController?.Close();
		}

		private void OpenSettingsPopup()
		{
			settingsPopupController.Open(() =>
			{
				CloseMap();
				CloseHeroPopup();
				CloseGear();
				CloseGoldPopup();
			});
			Render(session.Capture());
		}

		private void CloseSettingsPopup()
		{
			settingsPopupController?.Close();
		}

		private void BuildAwayPopup(IdleAwayReport away)
		{
			if (away.HasAnything == false)
			{
				return;
			}

			AwayReportPresenter.Bind(UsePopup("away-popup-host"), away, uiContentAsset);
		}

		private void OpenHeroPopup(int slot)
		{
			if (slot < 0 || slot >= session.Capture().Party.Length)
			{
				slot = 0;
			}

			gearSeat = slot;
			CloseGear();
			CloseGoldPopup();
			CloseSettingsPopup();
			heroSelectionController.Open(slot);
			Render(session.Capture());
		}

		private void CloseHeroPopup()
		{
			heroSelectionController?.Close();
		}

		private void RenderHeroPopup(IdleSnapshot snapshot)
		{
			heroSelectionController?.Render(snapshot);
		}

		/// <summary>이 부위에 낄 수 있는 가방 아이템만 보여준다</summary>
		private void OpenGear(int slot)
		{
			if (gearHeroId < 0)
			{
				SayOnce(uiContentAsset.SelectHeroBeforeGearText, runtimeSettingsAsset.NoteSeconds);
				return;
			}

			CloseHeroPopup();
			CloseGoldPopup();
			CloseSettingsPopup();
			gearSelectionController.Open(slot);
			Render(session.Capture());
		}

		private void CloseGear()
		{
			gearSelectionController?.Close();
		}

		private void RenderGear(IdleSnapshot snapshot)
		{
			int heroId = gearHeroId;
			IdleItem equipped = heroId >= 0 && gearSelectionController != null
				&& gearSelectionController.SelectedSlot >= 0
				? session.WornOf(heroId, gearSelectionController.SelectedSlot)
				: default;
			gearSelectionController?.Render(snapshot, equipped, heroId);
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (built == false)
			{
				return;
			}

			battleHudController.Render(snapshot);
			goldDetailsController.Render(snapshot);
			settingsPopupController.Render(snapshot);

			RenderHand(snapshot);
			RenderTabBadges(snapshot);

			if (mapSelectionController.IsOpen)
			{
				mapSelectionController.Render(snapshot);
			}

			if (screenLayoutController.ContentVisible)
			{
				RenderPage(snapshot);
				RenderGear(snapshot);
				RenderHeroPopup(snapshot);
			}

		}

		private void RenderHand(IdleSnapshot snapshot)
		{
			cardHandController.Render(snapshot);
		}

		private void RenderTabBadges(IdleSnapshot snapshot)
		{
			sidePanelController.RenderBadges(
				snapshot, (int)openTab, screenLayoutController.ContentVisible);
		}

		private void RenderPage(IdleSnapshot snapshot)
		{
			switch (openTab)
			{
				case Tab.Doll: dollPageController.Render(snapshot); break;
				case Tab.Item: itemPageController.Render(snapshot); break;
				case Tab.Codex: codexPageController.Render(snapshot); break;
				case Tab.Shop: shopPageController.Render(snapshot); break;
				case Tab.Lab: labPageController.Render(snapshot); break;
				case Tab.Invest: investPageController.Render(snapshot); break;
				case Tab.Dungeon: dungeonPageController.Render(snapshot); break;
				default: break;
			}
		}

		/// <summary>자동 시전 켜고 끄기 (P1-6)</summary>
		private void ToggleAutoCast()
		{
			if (session == null)
			{
				return;
			}

			session.ToggleAutoCast();
			Render(session.Capture());
		}

		// ── 화면 상태 ─────────────────────────────────────────────────────

		private void OpenTab(Tab tab)
		{
			openTab = tab;
			heroSelectionController?.ClearSelection();

			// 상점, 연구소는 왼쪽 씬이 바뀐다 (layout.md §2). 지금은 덮개
			bool altScene = tab == Tab.Shop || tab == Tab.Lab;
			battleHudController.SetAlternateScene(altScene,
				uiContentAsset.ScenePlaceholderText(tab == Tab.Shop));

			screenLayoutController.OpenSide((int)openTab);
			Render(session.Capture());
		}

		private void CloseSide()
		{
			battleHudController.SetAlternateScene(false, string.Empty);
			screenLayoutController.CloseSide((int)openTab);
		}

		private void ToggleSplit()
		{
			screenLayoutController.ToggleSplit((int)openTab);
			Render(session.Capture());
		}

		private void ToggleMap()
		{
			mapSelectionController.Toggle(() =>
			{
				CloseHeroPopup();
				CloseGear();
				CloseGoldPopup();
				CloseSettingsPopup();
			});
			Render(session.Capture());
		}

		private void CloseMap()
		{
			mapSelectionController?.Close();
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

		// ── 의도 ──────────────────────────────────────────────────────────

		private void OnTapped(PointerDownEvent moment)
		{
			if (moment.target is Button || (moment.target is VisualElement element && IsInsideBox(element)))
			{
				return;
			}

			session.Send(new IdleTapIntent());

			if (stage != null)
			{
				stage.OnTap();
			}

			Render(session.Capture());
		}

		private static bool IsInsideBox(VisualElement element)
		{
			for (VisualElement at = element; at != null; at = at.parent)
			{
				if (at.ClassListContains("idle-box"))
				{
					return true;
				}
			}

			return false;
		}

		private void Cast(int handIndex)
		{
			IdleSnapshot beforeCast = session.Capture();
			if (handIndex < 0 || handIndex >= beforeCast.Cards.Length)
			{
				return;
			}

			IdleCardKind selected = beforeCast.Cards[handIndex].Kind;
			if (selected == IdleCardKind.Volley)
			{
				cardHandController.CancelAim();
				SayOnce(uiContentAsset.VolleyDragHint, runtimeSettingsAsset.NoteSeconds);
				return;
			}

			if (session.TryCastCard(handIndex, out IdleCardResult result) == false)
			{
				return;
			}

			IdleCardKind kind = result.Kind;

			switch (kind)
			{
				case IdleCardKind.Volley:
					// Volley is target-only and is resolved by EndSkillDrag.
					SayOnce(uiContentAsset.VolleyResolvedFeedback, runtimeSettingsAsset.NoteSeconds);
					break;

				case IdleCardKind.Supply:
					if (stage != null) { stage.OnSupply((float)result.EffectSeconds); }
					SayOnce(uiContentAsset.SupplyFeedbackText(
						result.EffectSeconds, result.EffectMultiplier), runtimeSettingsAsset.NoteSeconds);
					break;

				default:
					if (stage != null) { stage.OnAppraise(); }
					SayOnce(result.HasRoll
						? uiContentAsset.AppraiseCardFeedbackText(result.Roll.Tier, result.Roll.Value, result.Roll.Replaced)
						: uiContentAsset.AppraiseCardEmptyFeedback, runtimeSettingsAsset.NoteSeconds);
					break;
			}

			WriteDown();
			Render(session.Capture());
		}

		private void NextStage()
		{
			if (session.Send(new IdleNextStageIntent()))
			{
				SayOnce(uiContentAsset.NextStageFeedback, runtimeSettingsAsset.NoteSeconds);
				WriteDown();
			}

			Render(session.Capture());
		}

		private void StepStage(int delta)
		{
			GoToStage(session.Capture().Stage + delta);
		}

		private void GoToStage(int target)
		{
			CloseMap();
			if (session.Send(new IdleGoToStageIntent(target)))
			{
				WriteDown();
			}

			Render(session.Capture());
		}

		private void ToggleHold()
		{
			session.Send(new IdleHoldStageIntent(session.Capture().HoldingStage == false));
			WriteDown();
			Render(session.Capture());
		}

		private void Equip(int bagIndex)
		{
			itemPageController.Equip(bagIndex);
		}

		private void ChooseHero(int id)
		{
			int slot = heroSelectionController != null ? heroSelectionController.SelectedSeat : -1;

			if (slot < 0)
			{
				slot = FirstEmptySeat();
			}

			if (slot < 0)
			{
				SayOnce(uiContentAsset.PartyFullFeedback, runtimeSettingsAsset.NoteSeconds);
				Render(session.Capture());
				return;
			}

			session.Send(new IdleSetPartyIntent(slot, id));
			gearSeat = slot;
			CloseHeroPopup();
			WriteDown();
			Render(session.Capture());
		}

		private int FirstEmptySeat()
		{
			IdleSnapshot now = session.Capture();

			for (int slot = 0; slot < now.Party.Length; slot++)
			{
				if (now.Party[slot] < 0)
				{
					return slot;
				}
			}

			return -1;
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

		private string WornTip(int slot)
		{
			return itemPageController.WornTip(slot);
		}

		// ── 잔손 ──────────────────────────────────────────────────────────

		private void SayOnce(string what, float seconds)
		{
			settingsPopupController.ShowNote(what, seconds);
		}

	}
}
