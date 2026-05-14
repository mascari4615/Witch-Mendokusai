using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	public class HotbarView : MonoBehaviour
	{
		public const string USS_CLASS = "wm-hotbar";

		private const int SLOT_COUNT = 9;

		[SerializeField] private Hotbar hotbar;

		private VisualElement hotbarContainer;
		private ItemGrid grid;
		private int selectedIndex = 0;
		private readonly Action[] hotbarSlotActions = new Action[SLOT_COUNT];

		private UIRoot uiRoot;
		private SOManager soManager;
		private InputManager inputManager;
		private GameModeManager gameModeManager;

		[Inject]
		public void Construct(UIRoot uiRoot, SOManager soManager, InputManager inputManager, GameModeManager gameModeManager)
		{
			this.uiRoot = uiRoot;
			this.soManager = soManager;
			this.inputManager = inputManager;
			this.gameModeManager = gameModeManager;
		}

		public int SelectedIndex => selectedIndex;
		public Item SelectedItem => Resolve()?.GetItem(selectedIndex);

		private void Start()
		{
			hotbarContainer = new VisualElement { name = "Hotbar" };
			hotbarContainer.AddToClassList(USS_CLASS);
			uiRoot.HudLayer.Add(hotbarContainer);

			grid = new ItemGrid();
			hotbarContainer.Add(grid);

			Hotbar bound = Resolve();
			grid.Bind(bound);

			RegisterInputs();
			SelectSlot(0);

			gameModeManager.OnModeChanged += OnGameModeChanged;
			OnGameModeChanged(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			grid?.Unbind();
			UnregisterInputs();

			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;
		}

		private void OnGameModeChanged(GameMode mode)
		{
			if (hotbarContainer == null)
				return;
			hotbarContainer.style.display = mode == GameMode.Default ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private Hotbar Resolve() => hotbar != null ? hotbar : soManager.Hotbar;

		private void RegisterInputs()
		{
			inputManager.RegisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, OnScroll);

			for (int i = 0; i < SLOT_COUNT; i++)
			{
				int slotIndex = i;
				hotbarSlotActions[i] = () => SelectSlot(slotIndex);
				inputManager.RegisterInputEvent(InputEventType.HotbarSlot1 + i, InputEventResponseType.Performed, hotbarSlotActions[i]);
			}
		}

		private void UnregisterInputs()
		{
			if (inputManager == null)
				return;

			inputManager.UnregisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, OnScroll);
			for (int i = 0; i < SLOT_COUNT; i++)
				inputManager.UnregisterInputEvent(InputEventType.HotbarSlot1 + i, InputEventResponseType.Performed, hotbarSlotActions[i]);
		}

		private void OnScroll(InputAction.CallbackContext ctx)
		{
			float y = ctx.ReadValue<Vector2>().y;
			int capacity = SLOT_COUNT;
			if (y > 0)
				SelectSlot((selectedIndex - 1 + capacity) % capacity);
			else if (y < 0)
				SelectSlot((selectedIndex + 1) % capacity);
		}

		private void SelectSlot(int index)
		{
			selectedIndex = Mathf.Clamp(index, 0, SLOT_COUNT - 1);

			if (grid == null)
				return;

			VisualElement.Hierarchy hierarchy = grid.hierarchy;
			for (int i = 0; i < hierarchy.childCount; i++)
			{
				if (hierarchy[i] is Slot slot)
					slot.SetSelected(i == selectedIndex);
			}
		}
	}
}
