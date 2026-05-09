using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class BuildingBarView : MonoBehaviour
	{
		public const string USS_CLASS = "wm-building-bar";

		private VisualElement container;
		private readonly List<Slot> slots = new();
		private readonly List<Building> cachedBuildings = new();
		private int selectedIndex = -1;

		private void Start()
		{
			container = new VisualElement { name = "BuildingBar" };
			container.AddToClassList(USS_CLASS);

			container.style.position = Position.Absolute;
			container.style.bottom = 24;
			container.style.left = 0;
			container.style.right = 0;
			container.style.flexDirection = FlexDirection.Row;
			container.style.justifyContent = Justify.Center;
			container.style.display = DisplayStyle.None;

			UIRoot.Instance.HudLayer.Add(container);

			GameModeManager.Instance.OnModeChanged += OnGameModeChanged;
			OnGameModeChanged(GameModeManager.Instance.CurrentMode);
		}

		private void OnDestroy()
		{
			if (GameModeManager.TryGetExistingInstance(out GameModeManager gameModeManager))
				gameModeManager.OnModeChanged -= OnGameModeChanged;

			if (container != null)
				container.RemoveFromHierarchy();
		}

		private void OnGameModeChanged(GameMode mode)
		{
			bool isBuildMode = mode == GameMode.Build;
			container.style.display = isBuildMode ? DisplayStyle.Flex : DisplayStyle.None;
			if (isBuildMode)
				Refresh();
		}

		private void Refresh()
		{
			cachedBuildings.Clear();
			cachedBuildings.AddRange(SOManager.Instance.DataSOs[typeof(Building)].Values.Cast<Building>());

			BuildSlots(cachedBuildings.Count);

			for (int i = 0; i < cachedBuildings.Count; i++)
			{
				Building building = cachedBuildings[i];
				slots[i].SetIcon(building.Sprite);
				slots[i].SetAmount(0);
				slots[i].SetSelected(i == selectedIndex);
				slots[i].SetTooltipData(building);
			}
		}

		private void BuildSlots(int count)
		{
			while (slots.Count > count)
			{
				Slot lastSlot = slots[^1];
				container.Remove(lastSlot);
				slots.RemoveAt(slots.Count - 1);
			}

			while (slots.Count < count)
			{
				int slotIndex = slots.Count;
				Slot newSlot = new();
				newSlot.SetIndex(slotIndex);
				newSlot.RegisterCallback<ClickEvent>(_ => OnSlotClick(slotIndex));
				container.Add(newSlot);
				slots.Add(newSlot);
			}
		}

		private void OnSlotClick(int index)
		{
			if (index < 0 || index >= cachedBuildings.Count)
				return;

			selectedIndex = index;
			BuildManager.Instance.SelectBuilding(cachedBuildings[index]);

			for (int i = 0; i < slots.Count; i++)
				slots[i].SetSelected(i == selectedIndex);
		}
	}
}
