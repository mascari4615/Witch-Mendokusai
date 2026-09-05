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

			VisualElement shell = root.Q<VisualElement>("shell");
			BuildBattle(shell);
			BuildSide(shell);
			screenLayoutController = new ScreenLayoutController(
				root, sidePanelController, battleHudController, content);
			BuildAuxiliaryPopups();
			BuildSelectionPopups();
			auxiliaryPopupCoordinator.ShowAway(UsePopup("away-popup-host"), away);

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
			screenLayoutController?.Dispose();
			modalController?.Dispose();
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
				battleActionController.CastVolleyAt);

			Button wipe = battle.Q<Button>("wipe-button");
			wipe.style.display = Application.isEditor || Debug.isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
			wipe.clicked += wipeAndRestart;
			cardHandController.BringAimToFront();
			wipe.BringToFront();
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
				viewAssets.RowButton,
				modalController,
				session,
				content,
				() => selectionPopupCoordinator.CloseAll(),
				battleActionController.GoToStage,
				RequestRender);
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
		}

		public void Render(IdleSnapshot snapshot)
		{
			if (built == false)
			{
				return;
			}

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

		private void SayOnce(string what, float seconds)
		{
			auxiliaryPopupCoordinator.ShowNote(what, seconds);
		}
	}
}
