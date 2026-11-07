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
		private const string USS_SLOT_SIGNATURE = USS_SLOT + "--signature";

		private readonly VisualElement signatureSlot = new() { name = "Signature", pickingMode = PickingMode.Ignore };
		private readonly List<VisualElement> slots = new();
		private EquipmentData shownSignature;
		private int shownCount = -1;

		public RunEquipmentHud()
		{
			name = "RunEquipmentHud";
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);
			signatureSlot.AddToClassList(USS_SLOT);
			signatureSlot.AddToClassList(USS_SLOT_FILLED);
			signatureSlot.AddToClassList(USS_SLOT_SIGNATURE);
			Add(signatureSlot);
		}

		// 고유 장비는 덱 0번 고정, 획득 칸 수에서 제외
		public void Refresh(EquipmentData signature, IReadOnlyList<EquipmentData> equipments, int slotCount)
		{
			if (shownSignature == signature && shownCount == equipments.Count && slots.Count == slotCount)
				return;
			shownSignature = signature;
			shownCount = equipments.Count;
			signatureSlot.style.backgroundImage = new StyleBackground(signature.Sprite);

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
