using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 상품 격자
	public partial class ShopView
	{
		private sealed class CellElements
		{
			public VisualElement Root;
			public VisualElement Box;
			public VisualElement Image;
			public ShopEmptyIcon Empty;
			public Label EmptyLabel;
			public VisualElement PriceRow;
			public VisualElement Nyang;
			public ShopOutlinedLabel Value;
			public ShopOutlinedLabel Original;
			public ShopOutlinedLabel Owned;
			public Label Badge;
		}

		private readonly List<CellElements> cells = new();
		private ScrollView gridScroll;
		private VisualElement gridContent;

		private void BuildGrid()
		{
			VisualElement panel = MakePanel("grid", stage, out VisualElement gridPanelBody);
			gridScroll = new ScrollView(ScrollViewMode.Vertical);
			gridScroll.AddToClassList(Class("scroll"));
			gridScroll.AddToClassList(Class("grid-scroll"));
			gridScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
			gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			gridContent = Make("grid-content", gridScroll.contentContainer);
			gridPanelBody.Add(gridScroll);
			FinishPanel(panel);
		}

		public void SetCells(IReadOnlyList<ShopCellModel> models, bool large)
		{
			gridContent.EnableInClassList(Class("grid-content--large"), large);

			while (cells.Count < models.Count)
				cells.Add(MakeCell(cells.Count));

			for (int i = 0; i < cells.Count; i++)
			{
				bool used = i < models.Count;
				SetVisible(cells[i].Root, used);
				if (used)
					ApplyCell(cells[i], models[i]);
			}
		}

		public void SetSelectedCell(int index)
		{
			for (int i = 0; i < cells.Count; i++)
				cells[i].Root.EnableInClassList(Class("cell--selected"), i == index);
		}

		public VisualElement CellElement(int index) => index >= 0 && index < cells.Count ? cells[index].Root : null;

		public void ScrollToCell(int index)
		{
			VisualElement cell = CellElement(index);
			if (cell != null && cell.layout.height > 0)
				gridScroll.ScrollTo(cell);
		}

		private CellElements MakeCell(int index)
		{
			CellElements cell = new();
			cell.Root = Make("cell", gridContent, picking: true);
			cell.Box = Make("icon-box", cell.Root);
			cell.Box.AddToClassList(Class("cell-box"));
			cell.Image = Make("icon-image", cell.Box);
			cell.Image.AddToClassList(Class("cell-image"));
			cell.Empty = MakeEmptyIcon(cell.Box);
			cell.EmptyLabel = MakeLabel("empty-label", cell.Box, "빈칸");
			cell.EmptyLabel.AddToClassList(Class("cell-empty-label"));

			cell.PriceRow = Make("cell-price", cell.Root);
			cell.Nyang = Make("nyang", cell.PriceRow);
			cell.Nyang.AddToClassList(Class("cell-nyang"));
			cell.Value = MakeOutlined("cell-value", cell.PriceRow);
			cell.Original = MakeOutlined("cell-original", cell.PriceRow);

			cell.Owned = MakeOutlined("cell-owned", cell.Root);
			cell.Badge = MakeLabel("cell-badge", cell.Root);

			cell.Root.RegisterCallback<PointerMoveEvent>(evt =>
			{
				if (evt.deltaPosition.sqrMagnitude > 0)
					CellHovered(index);
			});
			cell.Root.RegisterCallback<ClickEvent>(_ => CellClicked(index));
			return cell;
		}

		private static ShopEmptyIcon MakeEmptyIcon(VisualElement parent)
		{
			ShopEmptyIcon empty = new();
			empty.AddToClassList(Class("empty-icon"));
			parent.Add(empty);
			return empty;
		}

		private static void ApplyCell(CellElements cell, ShopCellModel model)
		{
			bool hasIcon = model.Icon != null;
			SetIcon(cell.Image, model.Icon);
			SetVisible(cell.Image, hasIcon);
			SetVisible(cell.Empty, hasIcon == false);
			SetVisible(cell.EmptyLabel, hasIcon == false);
			cell.Box.EnableInClassList(Class("icon-box--empty"), hasIcon == false);
			cell.Root.EnableInClassList(Class("cell--dimmed"), model.IsDimmed);

			SetVisible(cell.Owned, model.Owned > 0);
			cell.Owned.text = $"x{model.Owned.ToString(CultureInfo.InvariantCulture)}";

			SetVisible(cell.Badge, model.CartAmount > 0);
			cell.Badge.text = model.CartAmount.ToString(CultureInfo.InvariantCulture);
			cell.Badge.EnableInClassList(Class("cell-badge--sell"), model.IsSellSide);

			bool isFree = model.PriceKind == ShopPriceKind.Free;
			SetVisible(cell.Nyang, isFree == false);
			SetVisible(cell.Original, isFree);
			cell.Original.text = model.Price.ToString(CultureInfo.InvariantCulture);

			cell.Value.EnableInClassList(Class("cell-value--no"), model.PriceKind == ShopPriceKind.BuyUnaffordable);
			cell.Value.EnableInClassList(Class("cell-value--sell"), model.PriceKind == ShopPriceKind.Sell);
			switch (model.PriceKind)
			{
				case ShopPriceKind.Free:
					cell.Value.text = "무료";
					break;
				case ShopPriceKind.Sell:
					cell.Value.text = $"+{model.Price.ToString(CultureInfo.InvariantCulture)}";
					break;
				default:
					cell.Value.text = model.Price.ToString(CultureInfo.InvariantCulture);
					break;
			}
		}
	}
}
