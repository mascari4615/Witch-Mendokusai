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
			cachedBuildings.AddRange(soManager.DataSOs[typeof(Building)].Values.Cast<Building>());

			List<ModeSelectionBar.Entry> entries = new();
			foreach (Building building in cachedBuildings)
			{
				entries.Add(new ModeSelectionBar.Entry(
					building.Name, 0, new Color(0.55f, 0.6f, 0.7f, 1f), building.Sprite, building));
			}

			selectionBar.SetEntries(entries);
			selectionBar.SetSelected(selectedIndex);
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
