using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 오른쪽 아래 장바구니
	public partial class ShopView
	{
		private sealed class CartRowElements
		{
			public VisualElement Root;
			public VisualElement Box;
			public VisualElement Image;
			public ShopEmptyIcon Empty;
			public Label Side;
			public Label Name;
			public Label Amount;
			public Label Total;
		}

		private readonly List<CartRowElements> cartRows = new();
		private ScrollView cartScroll;
		private Label cartEmpty;
		private Label cartTotal;
		private VisualElement afterRow;
		private Label afterBefore;
		private Label afterArrow;
		private Label afterValue;
		private Label afterShort;
		private VisualElement payButton;
		private Label payLabel;

		public VisualElement PayElement => payButton;

		private void BuildCart()
		{
			VisualElement panel = MakePanel("cart", stage, out VisualElement body);
			MakeOutlined("cart-title", body, "< 장바구니 >");

			VisualElement list = Make("cart-list", body);
			cartEmpty = MakeLabel("cart-empty", list, "Z로 담습니다.\n결제 전까지 지갑은 그대로입니다.");
			cartScroll = new ScrollView(ScrollViewMode.Vertical);
			cartScroll.AddToClassList(Class("scroll"));
			cartScroll.AddToClassList(Class("cart-scroll"));
			cartScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
			cartScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			list.Add(cartScroll);

			VisualElement summary = Make("summary", body);
			VisualElement totalRow = Make("summary-row", summary);
			MakeLabel("summary-label", totalRow, "합계");
			cartTotal = MakeLabel("summary-total", totalRow);

			VisualElement walletRow = Make("summary-row", summary);
			walletRow.AddToClassList(Class("summary-row--after"));
			MakeLabel("summary-label", walletRow, "결제 후 지갑");
			afterRow = Make("after", walletRow);
			afterBefore = MakeLabel("after-before", afterRow);
			afterArrow = MakeLabel("after-arrow", afterRow, "→");
			afterValue = MakeLabel("after-value", afterRow);
			afterShort = MakeLabel("after-short", afterRow);

			payButton = Make("pay", summary, picking: true);
			MakeLabel("pay-key", payButton, "Z");
			payLabel = MakeLabel("pay-label", payButton, "결제");
			payButton.RegisterCallback<PointerMoveEvent>(evt =>
			{
				if (evt.deltaPosition.sqrMagnitude > 0)
					PayHovered();
			});
			payButton.RegisterCallback<ClickEvent>(_ => PayClicked());

			FinishPanel(panel);
		}

		private CartRowElements MakeCartRow(int index)
		{
			CartRowElements row = new();
			row.Root = Make("cart-row", cartScroll.contentContainer, picking: true);
			row.Box = Make("icon-box", row.Root);
			row.Box.AddToClassList(Class("cart-box"));
			row.Image = Make("icon-image", row.Box);
			row.Image.AddToClassList(Class("cart-image"));
			row.Empty = MakeEmptyIcon(row.Box);
			row.Side = MakeLabel("cart-side", row.Root);
			row.Name = MakeLabel("cart-name", row.Root);
			row.Amount = MakeLabel("cart-amount", row.Root);
			row.Total = MakeLabel("cart-total", row.Root);
			row.Root.RegisterCallback<PointerMoveEvent>(evt =>
			{
				if (evt.deltaPosition.sqrMagnitude > 0)
					CartRowHovered(index);
			});
			row.Root.RegisterCallback<ClickEvent>(_ => CartRowClicked(index));
			return row;
		}

		public VisualElement CartRowElement(int index) => index >= 0 && index < cartRows.Count ? cartRows[index].Root : null;

		public void SetCart(ShopCartModel model)
		{
			SetVisible(cartEmpty, model.Rows.Count == 0);
			SetVisible(cartScroll, model.Rows.Count > 0);

			while (cartRows.Count < model.Rows.Count)
				cartRows.Add(MakeCartRow(cartRows.Count));

			for (int i = 0; i < cartRows.Count; i++)
			{
				bool used = i < model.Rows.Count;
				SetVisible(cartRows[i].Root, used);
				if (used)
					ApplyCartRow(cartRows[i], model.Rows[i]);
			}

			string sign = model.Net > 0 ? "+" : string.Empty;
			cartTotal.text = $"{sign}{model.Net.ToString("N0", CultureInfo.InvariantCulture)}냥";
			bool isOver = model.WalletAfter < 0;
			cartTotal.EnableInClassList(Class("summary-total--over"), isOver);
			cartTotal.EnableInClassList(Class("summary-total--gain"), isOver == false && model.Net >= 0);

			SetVisible(afterBefore, isOver == false);
			SetVisible(afterArrow, isOver == false);
			SetVisible(afterValue, isOver == false);
			SetVisible(afterShort, isOver);
			afterBefore.text = Number(model.Wallet);
			afterValue.text = Number(model.WalletAfter);
			afterShort.text = isOver ? $"{Number(-model.WalletAfter)}냥 부족" : string.Empty;

			payButton.EnableInClassList(Class("pay--off"), model.CanPay == false);
			payLabel.text = isOver ? "냥이 부족합니다." : "결제";
		}

		public void SetSelectedCartRow(int index)
		{
			for (int i = 0; i < cartRows.Count; i++)
				cartRows[i].Root.EnableInClassList(Class("cart-row--selected"), i == index);
			VisualElement selected = CartRowElement(index);
			if (selected != null && selected.layout.height > 0)
				cartScroll.ScrollTo(selected);
		}

		private static void ApplyCartRow(CartRowElements row, ShopCartRowModel model)
		{
			bool hasIcon = model.Icon != null;
			SetIcon(row.Image, model.Icon);
			SetVisible(row.Image, hasIcon);
			SetVisible(row.Empty, hasIcon == false);
			row.Box.EnableInClassList(Class("icon-box--empty"), hasIcon == false);

			row.Side.text = model.IsSell ? "판매" : "구매";
			row.Side.EnableInClassList(Class("cart-side--sell"), model.IsSell);
			row.Name.text = model.Name;
			row.Amount.text = $"x{model.Amount.ToString(CultureInfo.InvariantCulture)}";
			row.Total.text = $"{(model.IsSell ? "+" : "-")}{Number(model.Total)}";
			row.Total.EnableInClassList(Class("cart-total--sell"), model.IsSell);
		}
	}
}
