using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal enum ManagementPage
	{
		Doll = 0,
		Item = 1,
		Codex = 2,
		Shop = 3,
		Lab = 4,
		Dungeon = 5,
		Invest = 6,
	}

	internal sealed class SidePagesController
	{
		private readonly DollPageController dollPage;
		private readonly ItemPageController itemPage;
		private readonly CodexPageController codexPage;
		private readonly ShopPageController shopPage;
		private readonly LabPageController labPage;
		private readonly DungeonPageController dungeonPage;
		private readonly InvestPageController investPage;

		public SidePagesController(
			SidePanelController sidePanel,
			VisualElement root,
			IdleSession session,
			UIContentSO content,
			IdleViewAssetsSO viewAssets,
			HeroVisualPresenter heroVisualPresenter,
			GearVisualPresenter gearVisualPresenter,
			Func<int> selectedHeroId,
			Func<int> selectedGearSeat,
			Func<int> selectingPartySeat,
			Action<int> openHero,
			Action<int> openGear,
			Action openOdds,
			Action<VisualElement, Func<string>> hookTooltip,
			Action writeDown,
			Action requestRender,
			Action<string, float> showNote,
			Action playGood,
			float noteSeconds,
			long lockHoldMilliseconds)
		{
			itemPage = new ItemPageController(
				Bind(sidePanel, root, ManagementPage.Item, "item-page-host"),
				session,
				content,
				gearVisualPresenter,
				viewAssets.BagCell,
				viewAssets.ForgeKind,
				viewAssets.RowButton,
				selectedHeroId,
				writeDown,
				requestRender,
				showNote,
				hookTooltip,
				noteSeconds,
				lockHoldMilliseconds);
			dollPage = new DollPageController(
				Bind(sidePanel, root, ManagementPage.Doll, "doll-page-host"),
				session,
				content,
				heroVisualPresenter,
				gearVisualPresenter,
				selectedHeroId,
				selectedGearSeat,
				selectingPartySeat,
				openHero,
				openGear,
				itemPage.WornTip,
				hookTooltip,
				writeDown,
				requestRender,
				playGood);
			codexPage = new CodexPageController(
				Bind(sidePanel, root, ManagementPage.Codex, "codex-page-host"),
				viewAssets.RowLabel,
				content);
			shopPage = new ShopPageController(
				Bind(sidePanel, root, ManagementPage.Shop, "shop-page-host"),
				session, content, heroVisualPresenter, openOdds, writeDown, requestRender, showNote, noteSeconds);
			labPage = new LabPageController(
				Bind(sidePanel, root, ManagementPage.Lab, "lab-page-host"),
				session, content, writeDown, requestRender, showNote, noteSeconds);
			dungeonPage = new DungeonPageController(
				Bind(sidePanel, root, ManagementPage.Dungeon, "dungeon-page-host"), content);
			investPage = new InvestPageController(
				Bind(sidePanel, root, ManagementPage.Invest, "invest-page-host"),
				viewAssets.ProducerRow, session, content, requestRender);
		}

		public ItemPageController ItemPage => itemPage;

		public void Render(ManagementPage page, IdleSnapshot snapshot)
		{
			switch (page)
			{
				case ManagementPage.Doll: dollPage.Render(snapshot); break;
				case ManagementPage.Item: itemPage.Render(snapshot); break;
				case ManagementPage.Codex: codexPage.Render(snapshot); break;
				case ManagementPage.Shop: shopPage.Render(snapshot); break;
				case ManagementPage.Lab: labPage.Render(snapshot); break;
				case ManagementPage.Dungeon: dungeonPage.Render(snapshot); break;
				case ManagementPage.Invest: investPage.Render(snapshot); break;
				default: break;
			}
		}

		private static VisualElement Bind(
			SidePanelController sidePanel,
			VisualElement root,
			ManagementPage page,
			string hostName)
		{
			return sidePanel.BindPage((int)page, hostName, root);
		}
	}
}
