using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 생활 화면의 틀. 왼쪽 목록 (칸 격자나 줄), 오른쪽 상세와 할 일
	// 1920x1080 고정 무대와 판, 꺾쇠, 분류 띠는 상점 부품을 그대로 씀. 값 표시만, 판정은 페이지
	public sealed class HomesteadPageView : VisualElement
	{
		private const string SHOP = "wm-shop__";
		private const string BLOCK = "wm-hs__";

		public event Action<int> EntryHovered = delegate { };
		public event Action<int> EntryClicked = delegate { };
		public event Action<int> TabClicked = delegate { };
		public event Action<int> ActionClicked = delegate { };
		public event Action CloseClicked = delegate { };

		private readonly VisualElement stage;
		private readonly VisualElement titleIcon;
		private readonly ShopOutlinedLabel title;
		private readonly Label status;
		private readonly ShopOutlinedLabel wallet;
		private readonly VisualElement tabRow;
		private readonly ScrollView scroll;
		private readonly VisualElement content;
		private readonly Label empty;
		private readonly VisualElement detailIcon;
		private readonly ShopOutlinedLabel detailTitle;
		private readonly Label detailNote;
		private readonly VisualElement bar;
		private readonly VisualElement barFill;
		private readonly Label barText;
		private readonly VisualElement lines;
		private readonly Label costTitle;
		private readonly VisualElement costs;
		private readonly VisualElement actions;

		private readonly List<VisualElement> entryElements = new();
		private readonly List<VisualElement> actionElements = new();

		public HomesteadPageView(HomesteadArt art)
		{
			AddToClassList("wm-shop");
			AddToClassList("wm-hs");
			focusable = true;
			tabIndex = 0;
			styleSheets.Add(art.ShopStyleSheet);
			styleSheets.Add(art.StyleSheet);

			stage = new ShopGradientPanel { pickingMode = PickingMode.Ignore };
			stage.AddToClassList(SHOP + "stage");
			Add(stage);

			VisualElement head = Make(BLOCK + "head", stage);
			titleIcon = Make(BLOCK + "head-icon", head);
			title = MakeOutlined(BLOCK + "head-title", head);
			status = MakeLabel(BLOCK + "head-status", head);

			VisualElement walletRoot = Make(SHOP + "wallet", stage);
			ShopSlantFill band = new();
			band.AddToClassList(SHOP + "wallet-band");
			walletRoot.Add(band);
			Make(SHOP + "wallet-nyang", Make(SHOP + "wallet-cap", walletRoot));
			wallet = MakeOutlined(SHOP + "wallet-value", walletRoot);

			// 손가락에는 X 키가 없음. 냥 왼쪽에 닫기, 모양은 할 일 버튼과 같음
			VisualElement close = KeyHint.TouchOnly(Make(BLOCK + "close", stage, picking: true));
			ShopSlantFill closeFill = new();
			closeFill.AddToClassList(BLOCK + "action-fill");
			close.Add(closeFill);
			MakeLabel(BLOCK + "action-name", close, "닫기");
			close.RegisterCallback<ClickEvent>(_ => CloseClicked());

			tabRow = Make(SHOP + "tabs", stage);

			VisualElement list = MakePanel(BLOCK + "list", out VisualElement listBody);
			scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
			scroll.AddToClassList(SHOP + "scroll");
			listBody.Add(scroll);
			content = Make(BLOCK + "list-content", scroll.contentContainer);
			empty = MakeLabel(BLOCK + "empty", listBody);
			FinishPanel(list);

			VisualElement detail = MakePanel(BLOCK + "detail", out VisualElement detailBody);
			VisualElement detailHead = Make(BLOCK + "detail-head", detailBody);
			VisualElement iconBox = Make(BLOCK + "detail-icon-box", detailHead);
			detailIcon = Make(BLOCK + "detail-icon", iconBox);
			VisualElement texts = Make(BLOCK + "detail-texts", detailHead);
			detailTitle = MakeOutlined(BLOCK + "detail-title", texts);
			detailNote = MakeLabel(BLOCK + "detail-note", texts);
			bar = Make(BLOCK + "bar", detailBody);
			barFill = Make(BLOCK + "bar-fill", bar);
			barText = MakeLabel(BLOCK + "bar-text", bar);
			lines = Make(BLOCK + "lines", detailBody);
			costTitle = MakeLabel(BLOCK + "cost-title", detailBody);
			costs = Make(BLOCK + "costs", detailBody);
			Make(BLOCK + "spring", detailBody);
			actions = Make(BLOCK + "actions", detailBody);
			FinishPanel(detail);

			VisualElement keys = KeyHint.KeyOnly(Make(SHOP + "keys", stage));
			AddKey(keys, "방향키", "이동");
			AddKey(keys, "Z", "고르기");
			AddKey(keys, "X", "뒤로");
			AddKey(keys, "Tab", "분류");

			RegisterCallback<GeometryChangedEvent>(_ => FitStage());
		}

		private static VisualElement Make(string className, VisualElement parent, bool picking = false)
		{
			VisualElement made = new();
			made.AddToClassList(className);
			if (picking == false)
				made.pickingMode = PickingMode.Ignore;
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

		private static ShopOutlinedLabel MakeOutlined(string className, VisualElement parent)
		{
			ShopOutlinedLabel label = new();
			label.AddToClassList(className);
			parent.Add(label);
			return label;
		}

		private static void SetIcon(VisualElement image, Sprite sprite) =>
			image.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);

		private static void SetVisible(VisualElement element, bool visible) => element.EnableInClassList("wm-hs--none", visible == false);

		private VisualElement MakePanel(string className, out VisualElement body)
		{
			ShopGradientPanel made = new() { pickingMode = PickingMode.Ignore };
			made.AddToClassList(SHOP + "panel");
			made.AddToClassList(className);
			stage.Add(made);
			MakeNine(SHOP + "panel-targeting", made);
			body = Make(SHOP + "panel-body", made);
			body.AddToClassList(className + "-body");
			return made;
		}

		private static void FinishPanel(VisualElement made) => MakeNine(SHOP + "panel-corner", made);

		private static void MakeNine(string className, VisualElement parent)
		{
			ShopNineSlice frame = new();
			frame.AddToClassList(className);
			parent.Add(frame);
		}

		private static void AddKey(VisualElement keys, string key, string action)
		{
			VisualElement row = Make(SHOP + "key", keys);
			MakeLabel(SHOP + "key-cap", row, key);
			MakeLabel(SHOP + "key-action", row, action);
		}

		private void FitStage()
		{
			float width = stage.resolvedStyle.width;
			float height = stage.resolvedStyle.height;
			if (width <= 0 || height <= 0 || layout.width <= 0 || layout.height <= 0)
				return;
			float scale = Mathf.Min(layout.width / width, layout.height / height);
			stage.style.scale = new Scale(new Vector3(scale, scale, 1));
			stage.style.left = (layout.width - width * scale) / 2;
			stage.style.top = (layout.height - height * scale) / 2;
		}

		// ---- 채우기

		public void SetHead(string text, Sprite icon, string statusText, long nyang)
		{
			title.text = text;
			SetIcon(titleIcon, icon);
			status.text = statusText;
			wallet.text = $"{nyang.ToString("N0", CultureInfo.InvariantCulture)}냥";
		}

		public void SetTabs(IReadOnlyList<string> names, int selected)
		{
			tabRow.Clear();
			SetVisible(tabRow, names.Count > 1);
			for (int i = 0; i < names.Count; i++)
			{
				int index = i;
				VisualElement tab = Make(SHOP + "tab", tabRow, picking: true);
				ShopSlantFill fill = new();
				fill.AddToClassList(SHOP + "tab-fill");
				tab.Add(fill);
				VisualElement text = Make(SHOP + "tab-text", tab);
				MakeLabel(SHOP + "tab-name", text, names[i]);
				tab.EnableInClassList(SHOP + "tab--on", i == selected);
				tab.RegisterCallback<ClickEvent>(_ => TabClicked(index));
			}
		}

		public void SetEntries(IReadOnlyList<HomesteadEntry> entries, bool grid, string emptyText)
		{
			content.Clear();
			entryElements.Clear();
			content.EnableInClassList(BLOCK + "list-content--grid", grid);
			SetVisible(empty, entries.Count == 0);
			empty.text = emptyText;
			for (int i = 0; i < entries.Count; i++)
			{
				int index = i;
				VisualElement element = grid ? MakeCell(entries[i]) : MakeRow(entries[i]);
				element.RegisterCallback<PointerMoveEvent>(evt =>
				{
					if (evt.deltaPosition.sqrMagnitude > 0)
						EntryHovered(index);
				});
				element.RegisterCallback<ClickEvent>(_ => EntryClicked(index));
				entryElements.Add(element);
			}
		}

		private VisualElement MakeCell(HomesteadEntry entry)
		{
			VisualElement cell = Make(BLOCK + "cell", content, picking: true);
			cell.EnableInClassList(BLOCK + "cell--dim", entry.IsDim);
			cell.EnableInClassList(BLOCK + "cell--unknown", entry.IsUnknown);
			cell.EnableInClassList(BLOCK + "cell--marked", entry.IsMarked);
			VisualElement image = Make(BLOCK + "cell-image", cell);
			SetIcon(image, entry.Icon);
			Make(BLOCK + "cell-stand", cell);
			ShopOutlinedLabel count = MakeOutlined(BLOCK + "cell-count", cell);
			count.text = entry.Count;
			if (entry.IsMarked)
				Make(BLOCK + "mark", cell);
			return cell;
		}

		private VisualElement MakeRow(HomesteadEntry entry)
		{
			VisualElement row = Make(BLOCK + "row", content, picking: true);
			row.EnableInClassList(BLOCK + "row--dim", entry.IsDim);
			row.EnableInClassList(BLOCK + "row--marked", entry.IsMarked);
			VisualElement box = Make(BLOCK + "row-box", row);
			SetIcon(Make(BLOCK + "row-image", box), entry.Icon);
			VisualElement texts = Make(BLOCK + "row-texts", row);
			MakeLabel(BLOCK + "row-name", texts, entry.Name);
			MakeLabel(BLOCK + "row-sub", texts, entry.Sub);
			MakeLabel(BLOCK + "row-count", row, entry.Count);
			if (entry.IsMarked)
				Make(BLOCK + "mark", row);
			return row;
		}

		public void SelectEntry(int index, bool scrollTo)
		{
			for (int i = 0; i < entryElements.Count; i++)
				entryElements[i].EnableInClassList(BLOCK + "entry--on", i == index);
			if (scrollTo && index >= 0 && index < entryElements.Count)
			{
				VisualElement target = entryElements[index];
				scroll.schedule.Execute(() =>
				{
					if (target.panel != null && target.layout.height > 0)
						scroll.ScrollTo(target);
				});
			}
		}

		public int Columns
		{
			get
			{
				if (entryElements.Count == 0 || content.ClassListContains(BLOCK + "list-content--grid") == false)
					return 1;
				float width = entryElements[0].layout.width + entryElements[0].resolvedStyle.marginRight;
				return width <= 0f ? 1 : Mathf.Max(1, Mathf.FloorToInt(content.layout.width / width));
			}
		}

		public void SetDetail(HomesteadDetail detail, HomesteadCatalog catalog)
		{
			VisualElement body = lines.parent;
			SetVisible(body, detail != null);
			actionElements.Clear();
			if (detail == null)
				return;

			SetIcon(detailIcon, detail.Icon);
			detailTitle.text = detail.Title;
			detailNote.text = detail.Note;
			SetVisible(bar, detail.Bar >= 0f);
			if (detail.Bar >= 0f)
			{
				barFill.style.width = Length.Percent(Mathf.Clamp01(detail.Bar) * 100f);
				barText.text = detail.BarText;
			}

			lines.Clear();
			foreach (string line in detail.Lines)
				MakeLabel(BLOCK + "line", lines, line);

			SetVisible(costTitle, detail.Costs.Count > 0);
			costTitle.text = detail.CostTitle;
			costs.Clear();
			foreach (HomesteadCostLine cost in detail.Costs)
			{
				VisualElement row = Make(BLOCK + "cost", costs);
				row.EnableInClassList(BLOCK + "cost--no", cost.IsEnough == false);
				SetIcon(Make(BLOCK + "cost-icon", row), cost.Icon);
				MakeLabel(BLOCK + "cost-name", row, cost.Name);
				MakeLabel(BLOCK + "cost-value", row, $"{cost.Have} / {cost.Need}");
			}

			actions.Clear();
			for (int i = 0; i < detail.Actions.Count; i++)
			{
				int index = i;
				HomesteadAction action = detail.Actions[i];
				VisualElement button = Make(BLOCK + "action", actions, picking: true);
				ShopSlantFill fill = new();
				fill.AddToClassList(BLOCK + "action-fill");
				button.Add(fill);
				MakeLabel(BLOCK + "action-name", button, action.Label);
				button.EnableInClassList(BLOCK + "action--no", action.Can.Ok == false);
				if (action.Can.Ok == false)
					MakeLabel(BLOCK + "action-reason", button, HomesteadText.Reason(action.Can, catalog));
				button.RegisterCallback<ClickEvent>(_ => ActionClicked(index));
				actionElements.Add(button);
			}
		}

		public void SelectAction(int index)
		{
			for (int i = 0; i < actionElements.Count; i++)
				actionElements[i].EnableInClassList(BLOCK + "action--on", i == index);
		}

		// 못 한 이유를 냥 옆에 잠깐. 상점의 알림과 같은 모양
		public void Pop(string text, bool warning, int visibleMilliseconds, int fadeMilliseconds)
		{
			ShopOutlinedLabel pop = MakeOutlined(BLOCK + "pop", stage);
			pop.text = text;
			pop.EnableInClassList(BLOCK + "pop--warning", warning);
			pop.schedule.Execute(() => pop.AddToClassList(BLOCK + "pop--exit")).ExecuteLater(visibleMilliseconds);
			pop.schedule.Execute(pop.RemoveFromHierarchy).ExecuteLater(visibleMilliseconds + fadeMilliseconds);
		}
	}
}
