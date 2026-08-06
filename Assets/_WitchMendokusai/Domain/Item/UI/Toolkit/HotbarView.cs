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
			RegisterSlotTaps();
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

		/// <summary>
		/// 칸을 손가락으로 골라지게 한다 (TASK-WM-200).
		///
		/// ★ 여태 칸을 고르는 길은 **숫자키 1~9 와 휠**뿐이었다. 폰엔 둘 다 없다 — 즉 폰에서는
		///   *무엇을 들지 고르는 일 자체가 불가능*했고, 그래서 짓기·쓰기가 통째로 막혀 있었다
		///   (2026-08-07 실기: 개척에서 확대·축소 말고 아무것도 안 됐다).
		/// ★ 왜 새 뜻을 안 만드나: 톡 = 그 칸의 숫자키를 누른 것과 **같은 일**이다. 여기서 따로
		///   고르는 길을 만들면 키로 고른 것과 손가락으로 고른 것이 갈라질 수 있다.
		/// ★ 마우스에도 켠다 — 컴퓨터에서도 칸을 클릭해 고르는 건 자연스럽고, 손가락 전용으로 두면
		///   컴퓨터에서 폰 조작을 확인할 때 실제와 다르게 동작한다.
		/// </summary>
		private void RegisterSlotTaps()
		{
			if (grid == null)
				return;

			// ★ 칸 하나하나가 아니라 *바구니*에 건다. 칸은 소지품 크기가 바뀌면 통째로 다시 만들어지는데,
			//   그때 칸에 걸어둔 것은 같이 사라진다 — 「어제는 눌렸는데 오늘은 안 눌린다」가 되고,
			//   아무 에러도 안 난다. 바구니는 안 사라지므로 한 번 걸면 끝이다.
			// ★ **내려가는 길에** 잡는다(TrickleDown). 칸은 이미 자기 눌림을 처리하고 *거기서 전파를
			//   끊는다* — 올라오는 길에 걸면 이 손짓은 영영 안 온다. 실제로 처음에 그렇게 짰다가
			//   「걸어는 놨는데 한 번도 안 불리는 코드」가 될 뻔했다.
			// ★ 손가락일 때만 뜻을 바꾼다. 컴퓨터에서 칸 클릭은 예전부터 *물건을 집고 놓는* 일이고,
			//   그걸 빼앗으면 되던 것이 깨진다. 폰엔 그 길이 아예 없어서 고르기가 먼저다.
			grid.RegisterCallback<PointerDownEvent>(
				evt =>
				{
					if (inputManager == null || inputManager.IsTouchMode == false)
						return;
					if (evt.target is not VisualElement target)
						return;

					Slot slot = target as Slot ?? target.GetFirstAncestorOfType<Slot>();
					if (slot == null || slot.Index < 0 || slot.Index >= SLOT_COUNT)
						return;

					SelectSlot(slot.Index);
					// 칸을 고르려던 손가락이 그 아래 땅까지 누르면, 고르자마자 지어진다.
					evt.StopPropagation();
				},
				TrickleDown.TrickleDown);
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
