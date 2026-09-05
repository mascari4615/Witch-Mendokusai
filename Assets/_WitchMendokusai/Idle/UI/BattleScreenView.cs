using System;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 작전 화면의 UI 조립과 그리기. 판 하나(<see cref="VisualElement"/> root)에 컨트롤러 꽂기와 스냅샷 뿌리기
	///
	/// ★ 판이 바뀌면 통째로 버리고 새로 짓기. <see cref="BattleScreen"/> 은 수명주기와 저장, 여기는 화면만
	/// ★ 짓는 도중 그리기 요청은 무시. 아직 없는 조각(맵 팝업)에 닿아 죽음 (실측 2026-08-30)
	/// </summary>
	internal sealed class BattleScreenView
	{
		private readonly VisualElement root;
		private readonly IdleSession session;
		private readonly BattleStage stage;
		private readonly UIContentSO content;
		private readonly RuntimeSettingsSO settings;
		private readonly IdleViewAssetsSO viewAssets;
		private readonly HeroVisualPresenter heroVisualPresenter;
		private readonly GearVisualPresenter gearVisualPresenter;
		private readonly Action writeDown;
		private readonly Action wipeAndRestart;
		private readonly Action playGood;

		private VisualElement battle;
		private BattleHudController battleHudController;
		private BattleActionController battleActionController;
		private CardHandController cardHandController;
		private SidePanelController sidePanelController;
		private ScreenLayoutController screenLayoutController;
		private SidePagesController sidePagesController;
		private SelectionPopupCoordinator selectionPopupCoordinator;
		private AuxiliaryPopupCoordinator auxiliaryPopupCoordinator;
		private ModalController modalController;
		private PointerTooltipController tooltipController;
		private bool built;
		/// <summary>Esc 를 받는 자리. 판의 맨 위 (초점이 없으면 이벤트가 거기로 감)</summary>
		private VisualElement cancelTarget;
		/// <summary>구역이 바뀔 때 덮는 막. 0 에서 1 로 갔다 다시 0 으로</summary>
		private VisualElement stageVeil;
		private float veilLeft;
		private int veilStage = -1;
		/// <summary>화면 알림. 설정 팝업 로그와 <b>같은 말</b>을 전투 창에도 띄운다</summary>
		private Label battleNote;
		private float battleNoteLeft;
		private EventCallback<NavigationCancelEvent> onCancel;

		public BattleScreenView(
			VisualElement root,
			IdleSession session,
			BattleStage stage,
			UIContentSO content,
			RuntimeSettingsSO settings,
			IdleViewAssetsSO viewAssets,
			HeroVisualPresenter heroVisualPresenter,
			GearVisualPresenter gearVisualPresenter,
			ManagementPage openPage,
			Action writeDown,
			Action wipeAndRestart,
			Action playGood)
		{
			this.root = root;
			this.session = session;
			this.stage = stage;
			this.content = content;
			this.settings = settings;
			this.viewAssets = viewAssets;
			this.heroVisualPresenter = heroVisualPresenter;
			this.gearVisualPresenter = gearVisualPresenter;
			this.writeDown = writeDown;
			this.wipeAndRestart = wipeAndRestart;
			this.playGood = playGood;
			OpenedPage = openPage;
		}

		/// <summary>지금 펼친 관리 판. 판을 다시 지을 때 이어받는다</summary>
		public ManagementPage OpenedPage { get; private set; }

		/// <summary>카드 조준 중. 무대 시간이 느려짐</summary>
		public bool Aiming => cardHandController != null && cardHandController.IsAiming;

		// ── 짓기 ──────────────────────────────────────────────────────────

		public void Build(IdleAwayReport away)
		{
			modalController = new ModalController(root, settings.ModalRepaintMilliseconds);
			tooltipController = new PointerTooltipController(
				root.Q<Label>("tooltip"), settings.CreateTooltipLayout());
			battleActionController = new BattleActionController(
				session,
				stage,
				content,
				settings,
				() => cardHandController.CancelAim(),
				() => auxiliaryPopupCoordinator.CloseMap(),
				writeDown,
				RequestRender,
				SayOnce);

			battleNote = root.Q<Label>("battle-note");
			stageVeil = root.Q<VisualElement>("stage-veil");
			VisualElement shell = root.Q<VisualElement>("shell");
			BuildBattle(shell);
			BuildSide(shell);
			screenLayoutController = new ScreenLayoutController(
				root, sidePanelController, battleHudController, content);
			BuildAuxiliaryPopups();
			BuildSelectionPopups();
			auxiliaryPopupCoordinator.ShowAway(UsePopup("away-popup-host"), away);
			HookCancel();

			if (stage != null)
			{
				stage.SetFloatingTextRoot(battle);
			}

			built = true;
			screenLayoutController.Apply((int)OpenedPage);
			ApplyScene(OpenedPage);
		}

		public void Dispose()
		{
			built = false;
			if (cancelTarget != null && onCancel != null)
			{
				cancelTarget.UnregisterCallback(onCancel, TrickleDown.TrickleDown);
			}

			cancelTarget = null;
			onCancel = null;
			screenLayoutController?.Dispose();
			modalController?.Dispose();
		}

		/// <summary>
		/// Esc (layout.md 구현 순서 11, 사용자 2026-09-05). 장치는 안 읽고 UI Toolkit 의 취소 이벤트만
		///
		/// ★ 순서: 조준 중이면 조준 취소, 팝업이 떠 있으면 전부 닫기, 아니면 설정 열기.
		///   취소 이벤트는 초점 요소가 없으면 판의 맨 위로 감. 거기서 TrickleDown 으로 받음
		/// </summary>
		private void HookCancel()
		{
			cancelTarget = root.panel != null ? root.panel.visualTree : root;
			onCancel = OnCancel;
			cancelTarget.RegisterCallback(onCancel, TrickleDown.TrickleDown);
		}

		private void OnCancel(NavigationCancelEvent moment)
		{
			moment.StopPropagation();
			if (cardHandController != null && cardHandController.IsAiming)
			{
				cardHandController.CancelAim();
				return;
			}

			if (modalController.IsAnyOpen)
			{
				auxiliaryPopupCoordinator.CloseAll();
				selectionPopupCoordinator.CloseAll();
				RequestRender();
				return;
			}

			auxiliaryPopupCoordinator.OpenSettings();
		}

		private void BuildBattle(VisualElement shell)
		{
			battle = shell.Q<VisualElement>("battle");

			// 빈 곳 누르기는 응원 한 대. 무대 그 자체가 큰 버튼
			battle.RegisterCallback<PointerDownEvent>(battleActionController.OnBattleTapped);

			battleHudController = new BattleHudController(
				battle,
				viewAssets.WaveDot,
				content,
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
				viewAssets.Card,
				viewAssets.QueueChip,
				content,
				battleActionController.CanAimCard,
				battleActionController.Cast,
				battleActionController.PickFoe,
				battleActionController.CastVolleyAt,
				battleActionController.AimAt,
				battleActionController.VolleyMissed);

			cardHandController.BringAimToFront();
		}

		private void BuildSide(VisualElement shell)
		{
			sidePanelController = new SidePanelController(
				shell, content,
				index => OpenPage((ManagementPage)index));
			sidePagesController = new SidePagesController(
				sidePanelController,
				root,
				session,
				content,
				viewAssets,
				heroVisualPresenter,
				gearVisualPresenter,
				() => selectionPopupCoordinator.HeroId,
				() => selectionPopupCoordinator.GearSeat,
				() => selectionPopupCoordinator.SelectingPartySeat,
				slot => selectionPopupCoordinator.OpenHero(slot),
				slot => selectionPopupCoordinator.OpenGear(slot),
				() => auxiliaryPopupCoordinator.OpenOdds(),
				tooltipController.Bind,
				writeDown,
				RequestRender,
				SayOnce,
				playGood,
				settings.NoteSeconds,
				settings.BagLockHoldMilliseconds);
		}

		private void BuildAuxiliaryPopups()
		{
			auxiliaryPopupCoordinator = new AuxiliaryPopupCoordinator(
				UsePopup("map-popup-host"),
				UsePopup("gold-popup-host"),
				UsePopup("settings-popup-host"),
				UsePopup("odds-popup-host"),
				viewAssets.RowButton,
				modalController,
				session,
				content,
				() => selectionPopupCoordinator.CloseAll(),
				battleActionController.GoToStage,
				RequestRender,
				wipeAndRestart);
		}

		/// <summary>인형과 장비 고르기 팝업. 관리 열 위에 뜬다</summary>
		private void BuildSelectionPopups()
		{
			selectionPopupCoordinator = new SelectionPopupCoordinator(
				UsePopup("hero-popup-host"),
				UsePopup("gear-popup-host"),
				viewAssets.ChoiceCard,
				modalController,
				heroVisualPresenter,
				gearVisualPresenter,
				session,
				content,
				sidePagesController.ItemPage,
				auxiliaryPopupCoordinator.CloseGoldAndSettings,
				writeDown,
				RequestRender,
				SayOnce,
				settings.NoteSeconds);
		}

		private VisualElement UsePopup(string hostName)
		{
			VisualElement host = root.Q<VisualElement>(hostName);
			VisualElement popup = host.Q<VisualElement>("popup");
			popup.style.display = DisplayStyle.None;
			return popup;
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Tick(float delta)
		{
			auxiliaryPopupCoordinator?.Tick(delta);
			TickNote(delta);
			TickVeil(delta);
		}

		/// <summary>
		/// 구역 전환 막. 앞 절반은 어두워짐, 뒤 절반은 밝아짐
		///
		/// ★ 웨이브 사이는 이음새가 없어야 하지만 구역이 바뀌는 것은 다른 곳으로 간 것.
		///   끊어 주는 편이 나음 (사용자 2026-09-05)
		/// </summary>
		private void TickVeil(float delta)
		{
			if (stageVeil == null || veilLeft <= 0f)
			{
				return;
			}

			veilLeft -= delta;
			float half = settings.StageVeilSeconds * 0.5f;
			float shown = veilLeft > half
				? (settings.StageVeilSeconds - veilLeft) / half
				: veilLeft / half;
			stageVeil.style.opacity = Mathf.Clamp01(shown);
		}

		/// <summary>구역이 바뀌었으면 막을 친다</summary>
		private void WatchStage(IdleSnapshot snapshot)
		{
			if (stageVeil == null)
			{
				return;
			}

			if (veilStage < 0)
			{
				veilStage = snapshot.Stage;
				return;
			}

			if (veilStage != snapshot.Stage)
			{
				veilStage = snapshot.Stage;
				veilLeft = settings.StageVeilSeconds;
			}
		}

		/// <summary>화면 알림을 서서히 지운다. 마지막 1초는 흐려짐</summary>
		private void TickNote(float delta)
		{
			if (battleNote == null || battleNoteLeft <= 0f)
			{
				return;
			}

			battleNoteLeft -= delta;
			battleNote.style.opacity = battleNoteLeft < 1f ? battleNoteLeft : 1f;
			if (battleNoteLeft <= 0f)
			{
				battleNote.style.display = DisplayStyle.None;
			}
		}

		public void Render(IdleSnapshot snapshot)
		{
			if (built == false)
			{
				return;
			}

			WatchStage(snapshot);
			battleHudController.Render(snapshot);
			auxiliaryPopupCoordinator.Render(snapshot);
			cardHandController.Render(snapshot);
			sidePanelController.RenderBadges(
				snapshot, (int)OpenedPage, screenLayoutController.ContentVisible);

			if (screenLayoutController.ContentVisible)
			{
				sidePagesController.Render(OpenedPage, snapshot);
				selectionPopupCoordinator.Render(snapshot);
			}
		}

		private void RequestRender()
		{
			Render(session.Capture());
		}

		// ── 화면 상태 ─────────────────────────────────────────────────────

		private void OpenPage(ManagementPage page)
		{
			OpenedPage = page;
			selectionPopupCoordinator.ClearHeroSelection();

			ApplyScene(page);
			screenLayoutController.OpenSide((int)OpenedPage);
			RequestRender();
		}

		/// <summary>상점, 연구소는 왼쪽 3D 장면이 바뀜 (layout.md 2). 전투 HUD 는 숨고 돌아가기 버튼만</summary>
		private void ApplyScene(ManagementPage page)
		{
			bool altScene = page == ManagementPage.Shop || page == ManagementPage.Lab;
			battleHudController.SetAlternateScene(altScene,
				content.ScenePlaceholderText(page == ManagementPage.Shop));

			if (stage != null)
			{
				stage.ShowScene(page == ManagementPage.Shop
					? StageScene.Shop
					: page == ManagementPage.Lab ? StageScene.Lab : StageScene.Battle);
			}
		}

		private void ToggleSplit()
		{
			screenLayoutController.ToggleSplit((int)OpenedPage);
			RequestRender();
		}

		/// <summary>
		/// 한 번 말한다. 전투 창 알림과 설정 팝업 로그 <b>둘 다</b>
		///
		/// ★ 전에는 설정 팝업 로그에만 적혔다. 팝업을 안 열면 아무 반응이 없어 보여
		///   일제 사격 힌트도, 빗나감도 사람에게 안 닿았다 (사용자 2026-09-05)
		/// </summary>
		private void SayOnce(string what, float seconds)
		{
			auxiliaryPopupCoordinator.ShowNote(what, seconds);

			if (battleNote == null)
			{
				return;
			}

			battleNote.text = what;
			battleNote.style.opacity = 1f;
			battleNote.style.display = DisplayStyle.Flex;
			battleNoteLeft = seconds;
		}
	}
}
