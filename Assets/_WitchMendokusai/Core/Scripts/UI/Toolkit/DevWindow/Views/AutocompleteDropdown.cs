using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// Tab 자동완성 후보 dropdown. 명령행 위쪽에 anchored. 후보 1개면 띄우지 않고 즉시 입력 (CommandLineField 측 처리).
	/// </summary>
	public class AutocompleteDropdown : VisualElement
	{
		public const string USS_CLASS = "wm-dev-autocomplete";
		public const string USS_ITEM = "wm-dev-autocomplete__item";
		public const string USS_ITEM_SELECTED = "wm-dev-autocomplete__item--selected";

		private readonly List<Label> itemLabels = new();
		private string[] candidates = Array.Empty<string>();
		private int selectedIndex;

		public bool IsOpen => style.display != DisplayStyle.None;
		public string Prefix { get; private set; }
		public string SelectedCandidate => candidates.Length == 0 ? null : candidates[selectedIndex];

		public AutocompleteDropdown()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.display = DisplayStyle.None;
		}

		public void Show(string prefix, string[] cands, UnityEngine.Rect anchor)
		{
			Prefix = prefix;
			candidates = cands ?? Array.Empty<string>();
			selectedIndex = 0;

			Clear();
			itemLabels.Clear();

			for (int i = 0; i < candidates.Length; i++)
			{
				Label label = new(candidates[i]);
				label.AddToClassList(USS_ITEM);
				label.pickingMode = PickingMode.Ignore;
				itemLabels.Add(label);
				Add(label);
			}

			UpdateHighlight();

			// OverlayLayer 에 띄움 — anchor(CommandLine) 위쪽에 absolute 배치.
			const int WIDTH = 220;
			int height = candidates.Length * 22 + 10;
			style.position = Position.Absolute;
			style.left = anchor.x + 24; // prompt 너비만큼 indent
			style.top = anchor.y - height - 2; // 2px gap 위로
			style.width = WIDTH;
			style.height = height;
			style.display = DisplayStyle.Flex;
		}

		public void Hide()
		{
			style.display = DisplayStyle.None;
			candidates = Array.Empty<string>();
			selectedIndex = 0;
			Clear();
			itemLabels.Clear();
		}

		public void MoveSelection(int delta)
		{
			if (candidates.Length == 0)
				return;

			selectedIndex = (selectedIndex + delta + candidates.Length) % candidates.Length;
			UpdateHighlight();
		}

		private void UpdateHighlight()
		{
			for (int i = 0; i < itemLabels.Count; i++)
			{
				if (i == selectedIndex)
					itemLabels[i].AddToClassList(USS_ITEM_SELECTED);
				else
					itemLabels[i].RemoveFromClassList(USS_ITEM_SELECTED);
			}
		}
	}
}
