using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIHotbar : UIItemGrid
	{
		public int SelectedIndex { get; private set; } = 0;
		public event Action<int, Item> OnSelectedChanged = delegate { };

		public Hotbar Hotbar => DataBufferSO as Hotbar;
		public Item SelectedItem => Hotbar?.GetItem(SelectedIndex);

		private const int HOTBAR_SLOT_COUNT = 9;
		private readonly Action[] hotbarSlotActions = new Action[HOTBAR_SLOT_COUNT];

		private void Start()
		{
			Init();
			InputManager.Instance.RegisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, OnScroll);
			for (int i = 0; i < HOTBAR_SLOT_COUNT; i++)
			{
				int slotIndex = i;
				hotbarSlotActions[i] = () => SelectHotbarSlot(slotIndex);
				InputManager.Instance.RegisterInputEvent(InputEventType.HotbarSlot1 + i, InputEventResponseType.Performed, hotbarSlotActions[i]);
			}
		}

		private void OnDestroy()
		{
			InputManager.Instance.UnregisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, OnScroll);
			for (int i = 0; i < HOTBAR_SLOT_COUNT; i++)
				InputManager.Instance.UnregisterInputEvent(InputEventType.HotbarSlot1 + i, InputEventResponseType.Performed, hotbarSlotActions[i]);
		}

		public override void Init()
		{
			base.Init();

			Navigation noNav = new Navigation { mode = Navigation.Mode.None };
			foreach (UIHotbarSlot slot in Slots.OfType<UIHotbarSlot>())
				slot.SetNavigation(noNav);

			SelectHotbarSlot(0);
		}

		private void OnScroll(InputAction.CallbackContext ctx)
		{
			float y = ctx.ReadValue<Vector2>().y;
			int capacity = Hotbar.Capacity;
			if (y > 0) SelectHotbarSlot((SelectedIndex - 1 + capacity) % capacity);
			else if (y < 0) SelectHotbarSlot((SelectedIndex + 1) % capacity);
		}

		public void SelectHotbarSlot(int index)
		{
			foreach (UIHotbarSlot slot in Slots.OfType<UIHotbarSlot>())
				slot.SetSelected(false);

			SelectedIndex = Mathf.Clamp(index, 0, Hotbar.Capacity - 1);

			if (Slots.ElementAtOrDefault(SelectedIndex) is UIHotbarSlot selected)
				selected.SetSelected(true);

			EventSystem.current?.SetSelectedGameObject(null);
			OnSelectedChanged.Invoke(SelectedIndex, SelectedItem);
		}
	}
}
