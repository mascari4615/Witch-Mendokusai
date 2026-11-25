using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 방 꾸미기. 위에서 본 평면에 가구를 놓고 옮기고 돌림. 격자 선 없음
	// 방향키로 옮기고 Z 로 놓기, Tab 으로 돌리기, X 로 물리기. 마우스와 손가락은 끌기
	public sealed partial class UIRoomPanel : UIToolkitPanel, IUIPanelInputOwner
	{
		private enum Focus
		{
			Palette = 0,
			Plan = 1,
		}

		private const string SHOP = "wm-shop__";
		private const string BLOCK = "wm-hs-room__";

		[SerializeField] private float moveStep = 0.1f;
		[SerializeField] private float turnStep = 15f;
		[SerializeField] private int focusGuardMilliseconds = 200;

		[Inject] internal InputManager inputManager;

		public override string Name => "꾸미기";
		public bool OwnsTabInput => true;

		private HomesteadSim Sim => HomesteadManager.Instance.Sim;
		private HomesteadArt Art => HomesteadArt.Instance;

		private VisualElement view;
		private VisualElement stage;
		private VisualElement palette;
		private ScrollView paletteScroll;
		private VisualElement plan;
		private Label comfortText;
		private VisualElement comfortFill;
		private Label pieceText;
		private Label hintText;

		private readonly List<FurnitureDef> spare = new();
		private readonly Dictionary<int, VisualElement> pieceElements = new();
		private Focus focus;
		private int paletteIndex;
		private int pieceUid;

		// 지금 든 것. 새것이면 heldUid 0
		private FurnitureDef held;
		private int heldUid;
		private float heldX;
		private float heldZ;
		private float heldYaw;
		private VisualElement heldElement;

		private readonly Stack<List<PlacedFurniture>> undo = new();

		protected override void BuildUI(VisualElement content)
		{
			view = new VisualElement { focusable = true, tabIndex = 0 };
			view.AddToClassList("wm-shop");
			view.AddToClassList("wm-hs-room");
			view.styleSheets.Add(Art.ShopStyleSheet);
			view.styleSheets.Add(Art.StyleSheet);
			content.Add(view);
			Root.styleSheets.Add(Art.ShopStyleSheet);
			Root.AddToClassList("wm-shop-backdrop");

			stage = new ShopGradientPanel { pickingMode = PickingMode.Ignore };
			stage.AddToClassList(SHOP + "stage");
			view.Add(stage);
			VisualElement head = Make("wm-hs__head", stage);
			Make("wm-hs__head-icon", head).style.backgroundImage = new StyleBackground(Art.Icon("house"));
			ShopOutlinedLabel title = new();
			title.AddToClassList("wm-hs__head-title");
			title.text = "방 꾸미기";
			head.Add(title);
			pieceText = MakeLabel("wm-hs__head-status", head);

			VisualElement left = MakePanel(BLOCK + "palette", out VisualElement leftBody);
			paletteScroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
			paletteScroll.AddToClassList(SHOP + "scroll");
			leftBody.Add(paletteScroll);
			palette = Make(BLOCK + "palette-content", paletteScroll.contentContainer);
			FinishPanel(left);

			VisualElement right = MakePanel(BLOCK + "floor", out VisualElement rightBody);
			VisualElement comfort = Make(BLOCK + "comfort", rightBody);
			comfortText = MakeLabel(BLOCK + "comfort-text", comfort);
			VisualElement comfortBar = Make("wm-hs__bar", comfort);
			comfortFill = Make("wm-hs__bar-fill", comfortBar);
			VisualElement planBox = Make(BLOCK + "plan-box", rightBody);
			plan = new VisualElement();
			plan.AddToClassList(BLOCK + "plan");
			planBox.Add(plan);
			Make(BLOCK + "door", plan);
			hintText = MakeLabel(BLOCK + "hint", rightBody);
			VisualElement buttons = Make(BLOCK + "buttons", rightBody);
			AddButton(buttons, "돌리기", () => Turn(1));
			AddButton(buttons, "거두기", PickUpHeld);
			AddButton(buttons, "되돌리기", Undo);
			AddButton(buttons, "전부 거두기", PickUpAll);
			FinishPanel(right);

			VisualElement keys = KeyHint.KeyOnly(Make(SHOP + "keys", stage));
			AddKey(keys, "방향키", "옮기기");
			AddKey(keys, "Z", "들기, 놓기");
			AddKey(keys, "Tab", "돌리기");
			AddKey(keys, "X", "거두기, 뒤로");

			view.RegisterCallback<NavigationMoveEvent>(OnMove, TrickleDown.TrickleDown);
			view.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
			view.RegisterCallback<NavigationCancelEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
			view.RegisterCallback<GeometryChangedEvent>(_ => FitStage());
			planBox.RegisterCallback<GeometryChangedEvent>(_ => FitPlan());
			plan.RegisterCallback<PointerMoveEvent>(OnPlanPointerMove);
			plan.RegisterCallback<PointerUpEvent>(OnPlanPointerUp);
			view.schedule.Execute(RestoreLostFocus).Every(focusGuardMilliseconds);
		}

		private static VisualElement Make(string className, VisualElement parent)
		{
			VisualElement made = new() { pickingMode = PickingMode.Ignore };
			made.AddToClassList(className);
			parent.Add(made);
			return made;
		}

		private static Label MakeLabel(string className, VisualElement parent, string text = "")
		{
			Label label = new(text) { pickingMode = PickingMode.Ignore };
			label.AddToClassList(SHOP + "label");
			label.AddToClassList(className);
			parent.Add(label);
			return label;
		}

		private VisualElement MakePanel(string className, out VisualElement body)
		{
			ShopGradientPanel made = new() { pickingMode = PickingMode.Ignore };
			made.AddToClassList(SHOP + "panel");
			made.AddToClassList(className);
			stage.Add(made);
			body = Make(SHOP + "panel-body", made);
			body.AddToClassList(className + "-body");
			return made;
		}

		private static void FinishPanel(VisualElement panel)
		{
			ShopNineSlice frame = new();
			frame.AddToClassList(SHOP + "panel-corner");
			panel.Add(frame);
		}

		private static void AddKey(VisualElement keys, string key, string action)
		{
			VisualElement row = Make(SHOP + "key", keys);
			MakeLabel(SHOP + "key-cap", row, key);
			MakeLabel(SHOP + "key-action", row, action);
		}

		private static void AddButton(VisualElement parent, string label, System.Action run)
		{
			VisualElement button = Make(BLOCK + "button", parent);
			button.pickingMode = PickingMode.Position;
			ShopSlantFill fill = new();
			fill.AddToClassList("wm-hs__action-fill");
			button.Add(fill);
			MakeLabel("wm-hs__action-name", button, label);
			button.RegisterCallback<ClickEvent>(_ => run());
		}

		private void FitStage()
		{
			float width = stage.resolvedStyle.width;
			float height = stage.resolvedStyle.height;
			if (width <= 0 || height <= 0 || view.layout.width <= 0 || view.layout.height <= 0)
				return;
			float scale = Mathf.Min(view.layout.width / width, view.layout.height / height);
			stage.style.scale = new Scale(new Vector3(scale, scale, 1));
			stage.style.left = (view.layout.width - width * scale) / 2;
			stage.style.top = (view.layout.height - height * scale) / 2;
		}

		protected override void OnOpen()
		{
			inputManager.RegisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			undo.Clear();
			Drop();
			focus = Focus.Palette;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			FocusView();
		}

		protected override void OnClose()
		{
			inputManager.UnregisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			Drop();
			Root?.panel?.focusController?.focusedElement?.Blur();
			if (HomesteadManager.TryGetExistingInstance(out HomesteadManager manager) && manager.Sim != null)
				manager.Save();
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
		}

		private void FocusView() => view.schedule.Execute(() => view.Focus());

		private void RestoreLostFocus()
		{
			if (view.panel != null && Root.resolvedStyle.display == DisplayStyle.Flex && view.focusController.focusedElement == null)
				view.Focus();
		}

		public override void UpdateUI()
		{
			FitPlan();
			RefreshPalette();
			RebuildPieces();
			RefreshComfort();
		}

		private void RefreshPalette()
		{
			spare.Clear();
			foreach (FurnitureDef furniture in Sim.Catalog.Furniture)
			{
				if (Sim.SpareCount(furniture.Id) > 0)
					spare.Add(furniture);
			}
			paletteIndex = Mathf.Clamp(paletteIndex, 0, Mathf.Max(0, spare.Count - 1));
			palette.Clear();
			for (int i = 0; i < spare.Count; i++)
			{
				int index = i;
				VisualElement row = Make("wm-hs__row", palette);
				row.pickingMode = PickingMode.Position;
				VisualElement box = Make("wm-hs__row-box", row);
				Make("wm-hs__row-image", box).style.backgroundImage = new StyleBackground(Art.Icon(spare[i].Id));
				VisualElement texts = Make("wm-hs__row-texts", row);
				MakeLabel("wm-hs__row-name", texts, spare[i].Name);
				MakeLabel("wm-hs__row-sub", texts, $"{spare[i].Theme}, 아늑함 {spare[i].Comfort}");
				MakeLabel("wm-hs__row-count", row, Sim.SpareCount(spare[i].Id).ToString());
				row.RegisterCallback<ClickEvent>(_ =>
				{
					focus = Focus.Palette;
					paletteIndex = index;
					TakeFromPalette();
				});
			}
			if (spare.Count == 0)
				MakeLabel(BLOCK + "palette-empty", palette, "놓을 가구가 없습니다. 공작소에서 짓거나 박물관에서 얻습니다.");
			MarkPalette();
		}

		private void MarkPalette()
		{
			for (int i = 0; i < palette.childCount; i++)
				palette[i].EnableInClassList("wm-hs__entry--on", focus == Focus.Palette && held == null && i == paletteIndex);
			if (focus == Focus.Palette && paletteIndex < spare.Count)
			{
				VisualElement target = palette[paletteIndex];
				paletteScroll.schedule.Execute(() =>
				{
					if (target.panel != null && target.layout.height > 0)
						paletteScroll.ScrollTo(target);
				});
			}
			foreach (KeyValuePair<int, VisualElement> pair in pieceElements)
				pair.Value.EnableInClassList(BLOCK + "piece--on", focus == Focus.Plan && held == null && pair.Key == pieceUid);
			RefreshHint();
		}

		private void RefreshHint()
		{
			if (held != null)
			{
				HomesteadResult can = Sim.CanPlace(held.Id, heldX, heldZ, heldYaw, heldUid);
				hintText.text = can.Ok ? $"{held.Name}" : $"{held.Name}  {HomesteadText.Reason(can, Sim.Catalog)}";
				return;
			}
			hintText.text = focus == Focus.Palette ? "놓을 것을 고릅니다" : "옮길 것을 고릅니다";
		}

		private void RefreshComfort()
		{
			int rank = Sim.ComfortRank;
			List<int> steps = Sim.Catalog.Tuning.ComfortSteps;
			int from = steps[rank];
			bool hasNext = rank + 1 < steps.Count;
			comfortText.text = hasNext ? $"아늑함 {Sim.Comfort}, {rank}단, 다음 {steps[rank + 1]}" : $"아늑함 {Sim.Comfort}, {rank}단";
			comfortFill.style.width = Length.Percent(hasNext ? Mathf.Clamp01((Sim.Comfort - from) / (float)(steps[rank + 1] - from)) * 100f : 100f);
			pieceText.text = $"{Sim.HouseTier.Name}, 가구 {Sim.State.Placed.Count} / {Sim.HouseTier.PieceLimit}, 쉬면 하루 동안 두 배 얻을 확률 {Mathf.RoundToInt(rank * Sim.Catalog.Tuning.RestDoubleChancePerRank * 100f)}%";
		}

		// ---- 평면

		// ---- 조작

		private void OnMove(NavigationMoveEvent evt)
		{
			evt.StopPropagation();
			view.focusController?.IgnoreEvent(evt);
			Vector2 direction = evt.direction switch
			{
				NavigationMoveEvent.Direction.Up => Vector2.up,
				NavigationMoveEvent.Direction.Down => Vector2.down,
				NavigationMoveEvent.Direction.Left => Vector2.left,
				NavigationMoveEvent.Direction.Right => Vector2.right,
				_ => Vector2.zero,
			};
			if (held != null)
			{
				MoveHeld(heldX + direction.x * moveStep, heldZ + direction.y * moveStep);
				return;
			}
			if (focus == Focus.Palette)
				MovePalette(direction);
			else
				MovePlan(direction);
		}

		private void MovePalette(Vector2 direction)
		{
			if (direction.x > 0f && Sim.State.Placed.Count > 0)
			{
				focus = Focus.Plan;
				if (pieceElements.ContainsKey(pieceUid) == false)
					pieceUid = Sim.State.Placed[0].Uid;
			}
			else
			{
				paletteIndex = Mathf.Clamp(paletteIndex - (int)direction.y, 0, Mathf.Max(0, spare.Count - 1));
			}
			UiSfx.Play(UiSfxEvent.FocusMove);
			MarkPalette();
		}

		private void OnSubmit(NavigationSubmitEvent evt)
		{
			evt.StopPropagation();
			if (held != null)
				PutDown();
			else if (focus == Focus.Palette)
				TakeFromPalette();
			else if (pieceElements.ContainsKey(pieceUid))
				TakePiece(pieceUid);
		}

		private void OnTabInput() => Turn(inputManager.IsShiftHeld ? -1 : 1);

		// X 와 Esc. 든 것이 있으면 거두기, 평면이면 목록으로, 목록이면 닫기
		public bool HandleCancelInput()
		{
			if (held != null)
			{
				PickUpHeld();
				return true;
			}
			if (focus == Focus.Plan)
			{
				focus = Focus.Palette;
				UiSfx.Play(UiSfxEvent.Cancel);
				MarkPalette();
				return true;
			}
			UiSfx.Play(UiSfxEvent.WindowClose);
			return false;
		}
	}
}

