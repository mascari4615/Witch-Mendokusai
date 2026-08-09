using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	public class BuildingBarView : MonoBehaviour
	{
		public const string USS_CLASS = "wm-building-bar";

		// ★ 개척과 *같은* 선택 바를 쓴다 — 모드마다 툴바를 새로 만들면 칸 하나 늘릴 때 여러 곳을 고쳐야 하고
		//   생김새도 따로 논다(사용자 지적). 고르는 규칙은 모드마다 달라도 고르는 행위는 하나다.
		private ModeSelectionBar selectionBar;
		private readonly List<Building> cachedBuildings = new();
		private int selectedIndex = -1;

		private UIRoot uiRoot;
		private GameModeManager gameModeManager;
		private SOManager soManager;
		private BuildManager buildManager;

		[Inject]
		public void Construct(UIRoot uiRoot, GameModeManager gameModeManager, SOManager soManager, BuildManager buildManager)
		{
			this.uiRoot = uiRoot;
			this.gameModeManager = gameModeManager;
			this.soManager = soManager;
			this.buildManager = buildManager;
		}

		private void Start()
		{
			selectionBar = new ModeSelectionBar("BuildingBar");
			selectionBar.Root.AddToClassList(USS_CLASS);
			selectionBar.SetVisible(false);
			selectionBar.Selected += OnSlotClick;

			uiRoot.HudLayer.Add(selectionBar.Root);

			gameModeManager.OnModeChanged += OnGameModeChanged;
			OnGameModeChanged(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;

			if (selectionBar != null)
				selectionBar.Root.RemoveFromHierarchy();
		}

		private void OnGameModeChanged(GameMode mode)
		{
			bool isBuildMode = mode == GameMode.Build;
			selectionBar.SetVisible(isBuildMode);
			if (isBuildMode)
				Refresh();
		}

		private void Refresh()
		{
			cachedBuildings.Clear();

			List<ModeSelectionBar.Entry> entries = new();

			// ★ 세계에 붙어 있으면 <b>세계가 아는 것만</b> 늘어놓는다 (TASK-WM-217).
			//   자기 자산을 늘어놓으면 세계가 모르는 것을 고를 수 있고, 그건 내 화면에만 섰다가
			//   사라진다 — 사람은 「짓기가 고장 났다」로 읽는다. 재료도 같이 보여 준다
			//   (웹 창은 이미 「나무 0/2」를 보여 준다. 같은 세계라면 여기도 같아야 한다).
			if (DomainSDK.Building.SharedBuildChannelBridge.IsActive)
			{
				List<BuildOption> options = BuildAffordability.Options(
					DomainSDK.Building.SharedBuildChannelBridge.Channel.Catalog, CountInBag);

				foreach (BuildOption option in options)
				{
					Building asset = SOHelper.Get<Building>(option.BuildingId);
					if (asset == null)
						continue; // 모양을 못 찾으면 안 세운다 — 조용히 터지는 것보다 낫다.

					cachedBuildings.Add(asset);

					string cost = BuildAffordability.CostText(option, NameOfItem);
					string label = string.IsNullOrEmpty(cost) ? option.Name : option.Name + "  " + cost;

					// 재료가 모자란 칸은 흐리게 — 눌러도 되지만 왜 안 서는지 미리 보인다.
					Color tint = option.Affordable
						? new Color(0.55f, 0.6f, 0.7f, 1f)
						: new Color(0.4f, 0.32f, 0.32f, 1f);

					entries.Add(new ModeSelectionBar.Entry(label, option.CostAmount, tint, asset.Sprite, asset));
				}
			}
			else
			{
				cachedBuildings.AddRange(soManager.DataSOs[typeof(Building)].Values.Cast<Building>());
				foreach (Building building in cachedBuildings)
				{
					entries.Add(new ModeSelectionBar.Entry(
						building.Name, 0, new Color(0.55f, 0.6f, 0.7f, 1f), building.Sprite, building));
				}
			}

			selectionBar.SetEntries(entries);
			selectionBar.SetSelected(selectedIndex);
		}

		/// <summary>지금 가방에 그게 몇 개 있나 — 세계가 내려준 가방이 이미 화면에 반영돼 있다.</summary>
		private int CountInBag(int itemId)
		{
			return soManager.ItemInventory.CountByID(itemId);
		}

		private string NameOfItem(int itemId)
		{
			string fromWorld = DomainSDK.Building.SharedBuildChannelBridge.IsActive
				? DomainSDK.Building.SharedBuildChannelBridge.Channel.NameOfItem(itemId)
				: string.Empty;

			if (string.IsNullOrEmpty(fromWorld) == false)
				return fromWorld;

			ItemData item = SOHelper.Get<ItemData>(itemId);
			return item == null ? string.Empty : item.Name;
		}

		private void OnSlotClick(int index)
		{
			if (index < 0 || index >= cachedBuildings.Count)
				return;

			selectedIndex = index;
			buildManager.SelectBuilding(cachedBuildings[index]);
			selectionBar.SetSelected(selectedIndex);
		}
	}
}
