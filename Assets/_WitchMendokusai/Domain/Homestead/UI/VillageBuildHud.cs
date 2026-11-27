using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 하는 일 버튼이 부를 것. 키 (R F 우클릭 U F6) 와 같은 일
	public readonly struct VillageBuildActions
	{
		public readonly Action<PlaceableDef> Take;
		public readonly Action Turn;
		public readonly Action Remove;
		public readonly Action Drop;
		public readonly Action Undo;
		public readonly Action Leave;

		public VillageBuildActions(Action<PlaceableDef> take, Action turn, Action remove, Action drop, Action undo, Action leave)
		{
			Take = take;
			Turn = turn;
			Remove = remove;
			Drop = drop;
			Undo = undo;
			Leave = leave;
		}
	}

	// 건설 중 아래 막대. 놓을 것과 하는 일 안내
	// 하는 일 버튼은 손가락 몫이지만 마우스로도 누름. 키 글자는 KeyHint 로 따로 두어 손가락 모드에서 숨김
	public sealed class VillageBuildHud
	{
		private const string BLOCK = "wm-hs-build";
		private const int REASON_MILLISECONDS = 1800;
		private const string SHOP_CLASS = "wm-shop";

		private readonly VisualElement root;
		private readonly Label hint;
		private readonly Label touchHint;
		private readonly List<(PlaceableDef Def, VisualElement Slot, Label Cost)> slots = new();
		private readonly VisualElement undoButton;
		private readonly VisualElement dropButton;
		private IVisualElementScheduledItem reasonTimer;
		private string idleHint = string.Empty;
		private string idleTouchHint = string.Empty;

		public VillageBuildHud(VisualElement parent, IReadOnlyList<PlaceableDef> palette, int keyCount, VillageBuildActions actions)
		{
			root = Make("root", parent);
			// 상점 판의 색 이름 (--wm-shop-*) 이 이 클래스에 정의됨
			root.AddToClassList(SHOP_CLASS);
			hint = KeyHint.KeyOnly(new Label { pickingMode = PickingMode.Ignore });
			hint.AddToClassList(BLOCK + "__hint");
			root.Add(hint);
			touchHint = KeyHint.TouchOnly(new Label { pickingMode = PickingMode.Ignore });
			touchHint.AddToClassList(BLOCK + "__hint");
			root.Add(touchHint);
			VisualElement tools = Make("tools", root);
			AddTool(tools, "돌리기", "R", actions.Turn);
			AddTool(tools, "거두기", "F", actions.Remove);
			dropButton = AddTool(tools, "그만두기", "우클릭", actions.Drop);
			undoButton = AddTool(tools, "되돌리기", "U", actions.Undo);
			AddTool(tools, "마치기", "F6", actions.Leave);
			VisualElement bar = Make("bar", root);
			for (int i = 0; i < palette.Count; i++)
			{
				PlaceableDef placeable = palette[i];
				Button slot = AddButton(bar, "slot", () => actions.Take(placeable));
				VisualElement icon = Make("icon", slot);
				icon.style.backgroundImage = new StyleBackground(HomesteadArt.Instance.Icon(placeable.Id));
				KeyHint.KeyOnly(MakeLabel("number", slot, i < keyCount ? (i + 1).ToString() : string.Empty));
				MakeLabel("name", slot, placeable.Name);
				Label cost = MakeLabel("cost", slot, string.Empty);
				slots.Add((placeable, slot, cost));
			}
		}

		private static Button AddTool(VisualElement parent, string text, string key, Action onClick)
		{
			Button button = AddButton(parent, "tool", onClick);
			button.Add(new Label(text) { pickingMode = PickingMode.Ignore });
			Label keyLabel = KeyHint.KeyOnly(MakeLabel("key", button, key));
			keyLabel.pickingMode = PickingMode.Ignore;
			return button;
		}

		public void Remove() => root.RemoveFromHierarchy();

		private static VisualElement Make(string element, VisualElement parent)
		{
			VisualElement made = new() { pickingMode = PickingMode.Ignore };
			made.AddToClassList($"{BLOCK}__{element}");
			parent.Add(made);
			return made;
		}

		private static Label MakeLabel(string element, VisualElement parent, string text)
		{
			Label label = new(text) { pickingMode = PickingMode.Ignore };
			label.AddToClassList($"{BLOCK}__{element}");
			parent.Add(label);
			return label;
		}

		private static Button AddButton(VisualElement parent, string element, Action onClick)
		{
			Button button = new(onClick) { focusable = false };
			button.ClearClassList();
			button.AddToClassList($"{BLOCK}__{element}");
			parent.Add(button);
			return button;
		}

		public void Refresh(HomesteadSim sim, PlaceableDef held, bool isMoving, bool canUndo)
		{
			foreach ((PlaceableDef placeable, VisualElement slot, Label cost) in slots)
			{
				slot.EnableInClassList(BLOCK + "__slot--held", placeable == held && isMoving == false);
				slot.EnableInClassList(BLOCK + "__slot--poor", sim.CanMakeDecor(placeable.Id).Ok == false);
				cost.text = CostText(sim, placeable);
			}
			undoButton.SetEnabled(canUndo);
			dropButton.SetEnabled(held != null);
			idleHint = held == null ? "집기 좌클릭, Z   거두기 F   움직이기 WASD   돌아보기 Q E" : $"{held.Name}   놓기 좌클릭, Z   돌리기 R   그만두기 우클릭";
			idleTouchHint = held == null ? "칸을 톡 하면 집기   두 손가락으로 밀기, 비틀기, 오므리기" : $"{held.Name}   톡 하면 자리 보기, 같은 칸 한 번 더 톡 하면 놓기";
			ShowHint(idleHint, idleTouchHint, false);
		}

		private static string CostText(HomesteadSim sim, PlaceableDef placeable)
		{
			List<(string Name, int Have, int Need)> parts = new();
			foreach (KeyValuePair<string, int> pair in placeable.Cost.Goods)
				parts.Add((sim.Catalog.Get<GoodsDef>(pair.Key).Name, sim.Count(pair.Key), pair.Value));
			return BuildSlotText.Cost(parts);
		}

		public void ShowReason(string reason)
		{
			ShowHint(reason, reason, true);
			reasonTimer?.Pause();
			reasonTimer = root.schedule.Execute(() => ShowHint(idleHint, idleTouchHint, false));
			reasonTimer.ExecuteLater(REASON_MILLISECONDS);
		}

		private void ShowHint(string text, string touchText, bool isRefusal)
		{
			hint.text = text;
			hint.EnableInClassList(BLOCK + "__hint--no", isRefusal);
			touchHint.text = touchText;
			touchHint.EnableInClassList(BLOCK + "__hint--no", isRefusal);
		}
	}
}
