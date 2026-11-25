using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 건설 중 아래 막대. 놓을 것 아홉 칸과 하는 일 안내
	public sealed class VillageBuildHud
	{
		private const string BLOCK = "wm-hs-build";
		private const int REASON_MILLISECONDS = 1800;

		private readonly VisualElement root;
		private readonly Label hint;
		private readonly List<(PlaceableDef Def, VisualElement Slot, Label Cost)> slots = new();
		private readonly VisualElement undoButton;
		private IVisualElementScheduledItem reasonTimer;
		private string idleHint = string.Empty;

		public VillageBuildHud(VisualElement parent, IReadOnlyList<PlaceableDef> palette, Action<PlaceableDef> onTake, Action onUndo, Action onLeave)
		{
			root = Make("root", parent);
			hint = new Label { pickingMode = PickingMode.Ignore };
			hint.AddToClassList(BLOCK + "__hint");
			root.Add(hint);
			VisualElement bar = Make("bar", root);
			for (int i = 0; i < palette.Count; i++)
			{
				PlaceableDef placeable = palette[i];
				Button slot = AddButton(bar, "slot", () => onTake(placeable));
				Label number = new((i + 1).ToString());
				number.AddToClassList(BLOCK + "__number");
				slot.Add(number);
				Label title = new(placeable.Name);
				title.AddToClassList(BLOCK + "__name");
				slot.Add(title);
				Label cost = new();
				cost.AddToClassList(BLOCK + "__cost");
				slot.Add(cost);
				slots.Add((placeable, slot, cost));
			}
			undoButton = AddButton(bar, "tool", onUndo);
			undoButton.Add(new Label("되돌리기 U"));
			AddButton(bar, "tool", onLeave).Add(new Label("마치기 F6"));
		}

		public void Remove() => root.RemoveFromHierarchy();

		private static VisualElement Make(string element, VisualElement parent)
		{
			VisualElement made = new() { pickingMode = PickingMode.Ignore };
			made.AddToClassList($"{BLOCK}__{element}");
			parent.Add(made);
			return made;
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
			idleHint = held == null ? "집기 좌클릭, Z   거두기 F   움직이기 WASD   돌아보기 Q E" : $"{held.Name}   놓기 좌클릭, Z   돌리기 R   그만두기 우클릭";
			ShowHint(idleHint, false);
		}

		private static string CostText(HomesteadSim sim, PlaceableDef placeable)
		{
			List<string> parts = new();
			foreach (KeyValuePair<string, int> pair in placeable.Cost.Goods)
				parts.Add($"{sim.Catalog.Get<GoodsDef>(pair.Key).Name} {sim.Count(pair.Key)}/{pair.Value}");
			return string.Join("  ", parts);
		}

		public void ShowReason(string reason)
		{
			ShowHint(reason, true);
			reasonTimer?.Pause();
			reasonTimer = root.schedule.Execute(() => ShowHint(idleHint, false));
			reasonTimer.ExecuteLater(REASON_MILLISECONDS);
		}

		private void ShowHint(string text, bool isRefusal)
		{
			hint.text = text;
			hint.EnableInClassList(BLOCK + "__hint--no", isRefusal);
		}
	}
}
