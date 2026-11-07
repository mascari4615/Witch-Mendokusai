using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 판 안 장비 칸. 스타일은 RunEquipmentHud.uss
	public class RunEquipmentHud : VisualElement
	{
		private const string USS_ROOT = "wm-run-equipment";
		private const string USS_SLOT = USS_ROOT + "__slot";
		private const string USS_SLOT_FILLED = USS_SLOT + "--filled";

		private readonly List<VisualElement> slots = new();
		private int shownCount = -1;

		public RunEquipmentHud()
		{
			name = "RunEquipmentHud";
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);
		}

		public void Refresh(IReadOnlyList<EquipmentData> equipments, int slotCount)
		{
			if (shownCount == equipments.Count && slots.Count == slotCount)
				return;
			shownCount = equipments.Count;

			while (slots.Count < slotCount)
			{
				VisualElement slot = new() { pickingMode = PickingMode.Ignore };
				slot.AddToClassList(USS_SLOT);
				slots.Add(slot);
				Add(slot);
			}
			while (slots.Count > slotCount)
			{
				slots[^1].RemoveFromHierarchy();
				slots.RemoveAt(slots.Count - 1);
			}

			for (int i = 0; i < slots.Count; i++)
			{
				bool isFilled = i < equipments.Count;
				slots[i].EnableInClassList(USS_SLOT_FILLED, isFilled);
				slots[i].style.backgroundImage = isFilled ? new StyleBackground(equipments[i].Sprite) : new StyleBackground(StyleKeyword.None);
			}
		}

		public void SetVisible(bool isVisible) => style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
	}
}
