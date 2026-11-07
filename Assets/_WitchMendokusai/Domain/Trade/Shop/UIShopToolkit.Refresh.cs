using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// 판정 상태에서 화면 값 구성
	public partial class UIShopToolkit
	{
		private static readonly (SurvivalUIKind kind, string name)[] COMPARE_KINDS =
		{
			(SurvivalUIKind.Count, "수"),
			(SurvivalUIKind.Damage, "피해"),
			(SurvivalUIKind.Speed, "속도"),
			(SurvivalUIKind.Size, "크기"),
			(SurvivalUIKind.Advanced, "고급"),
		};

		private void RefreshAll(bool jumpCursor)
		{
			RebuildTabs();
			if (tabIndex >= shopTabs.Count)
				tabIndex = 0;
			selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, CurrentTab.Items.Count - 1));

			view.SetMerchant(npc.Name, npc.Sprite, npc.IsShopFree);
			view.SetWallet(Wallet);

			List<ShopTabModel> tabModels = new();
			foreach (ShopTab tab in shopTabs)
				tabModels.Add(new ShopTabModel { Name = tab.Name, Count = tab.Items.Count, IsSell = tab.IsSell });
			view.SetTabs(tabModels, tabIndex);

			List<ShopCellModel> cells = new();
			foreach (ItemData item in CurrentTab.Items)
				cells.Add(CellOf(item));
			int rows = Mathf.CeilToInt((float)cells.Count / settings.GridColumns);
			view.SetCells(cells, rows <= settings.LargeCellMaxRows);

			RefreshCart();
			RefreshSelection();
			view.SetCursorTarget(CursorTarget(), jumpCursor);
		}

		private void RefreshCart()
		{
			ShopCartModel model = new()
			{
				Wallet = Wallet,
				Net = cart.Net,
				WalletAfter = cart.WalletAfter(Wallet),
				CanPay = cart.CanCheckout(Wallet),
			};
			foreach (ShopCartLine line in cart.Lines)
			{
				ItemData item = ItemFromLine(line);
				model.Rows.Add(new ShopCartRowModel
				{
					Icon = item.Sprite,
					Name = item.Name,
					IsSell = line.Side == ShopCartSide.Sell,
					Amount = line.Amount,
					Total = line.Total,
				});
			}
			view.SetCart(model);
		}

		private void RefreshSelection()
		{
			view.SetSelectedCell(isCartFocused ? -1 : selectedIndex);
			view.SetSelectedCartRow(isCartFocused && cartIndex < cart.Lines.Count ? cartIndex : -1);
			view.SetInfo(InfoOf(InfoItem()));
			if (isCartFocused == false)
				view.ScrollToCell(selectedIndex);
			view.SetCursorTarget(CursorTarget(), jump: false);
		}

		private VisualElement CursorTarget()
		{
			if (isCartFocused == false)
				return view.CellElement(selectedIndex);
			return cartIndex >= cart.Lines.Count ? view.PayElement : view.CartRowElement(cartIndex);
		}

		private ItemData InfoItem()
		{
			if (isCartFocused && cartIndex < cart.Lines.Count)
				return ItemFromLine(cart.Lines[cartIndex]);
			return SelectedItem;
		}

		private void RebuildTabs()
		{
			shopTabs.Clear();
			List<ItemData> stock = npc.ItemDataBuffers[0].InitItems;
			shopTabs.Add(new ShopTab { Name = "전체", Items = stock });

			AddCategoryTab(stock, "무기", IsWeapon);
			AddCategoryTab(stock, "도구", IsTool);
			AddCategoryTab(stock, "소모품", item => item.Type == ItemType.Potion);
			AddCategoryTab(stock, "재료", item => item.Type == ItemType.Loot);

			shopTabs.Add(new ShopTab { Name = "판매", IsSell = true, Items = BagItems() });
		}

		private void AddCategoryTab(List<ItemData> stock, string name, Func<ItemData, bool> filter)
		{
			List<ItemData> items = stock.Where(filter).ToList();
			if (items.Count > 0)
				shopTabs.Add(new ShopTab { Name = name, Items = items });
		}

		private static bool IsWeapon(ItemData item) => item is EquipmentData equipment && equipment.EquipmentType == EquipmentType.Default;
		private static bool IsTool(ItemData item) => item is EquipmentData equipment && equipment.EquipmentType != EquipmentType.Default;

		private static string CategoryNameOf(ItemData item)
		{
			if (IsWeapon(item))
				return "무기";
			if (IsTool(item))
				return "도구";
			return item.Type == ItemType.Potion ? "소모품" : "재료";
		}

		private List<ItemData> BagItems()
		{
			Inventory inventory = soManager.ItemInventory;
			SortedDictionary<int, ItemData> items = new();
			for (int i = 0; i < inventory.Capacity; i++)
			{
				Item slot = inventory.GetItem(i);
				if (slot != null)
					items[slot.Data.ID] = (ItemData)slot.Data;
			}
			return items.Values.ToList();
		}

		private ShopCellModel CellOf(ItemData item)
		{
			bool isSell = CurrentTab.IsSell;
			int buyPrice = npc.GetShopPrice(item);
			bool isAffordable = isSell || buyPrice <= Wallet;

			ShopPriceKind priceKind;
			int shownPrice;
			if (isSell)
			{
				priceKind = ShopPriceKind.Sell;
				shownPrice = item.SalePrice;
			}
			else if (npc.IsShopFree)
			{
				priceKind = ShopPriceKind.Free;
				shownPrice = item.PurchasePrice;
			}
			else
			{
				priceKind = isAffordable ? ShopPriceKind.Buy : ShopPriceKind.BuyUnaffordable;
				shownPrice = buyPrice;
			}

			return new ShopCellModel
			{
				Icon = item.Sprite,
				Owned = OwnedOf(item.ID),
				PriceKind = priceKind,
				Price = shownPrice,
				CartAmount = cart.AmountOf(item.ID, CurrentSide),
				IsSellSide = isSell,
				IsDimmed = isAffordable == false,
			};
		}

		private ShopInfoModel InfoOf(ItemData item)
		{
			if (item == null)
				return null;

			ShopInfoModel model = new()
			{
				Icon = item.Sprite,
				Name = item.Name,
				Description = item.Description,
				HasDescription = string.IsNullOrEmpty(item.Description) == false,
				IsWorn = WornEquipment().Any(worn => worn.ID == item.ID),
				CategoryName = CategoryNameOf(item),
				Owned = OwnedOf(item.ID),
				MaxAmount = item.MaxAmount,
				SalePrice = item.SalePrice,
			};

			if (item is EquipmentData equipment && equipment.EffectCards != null && equipment.EffectCards.Count > 0)
				model.CompareRows = CompareRowsOf(equipment);
			return model;
		}

		private List<EquipmentData> WornEquipment()
		{
			List<EquipmentData> worn = new();
			foreach (Item slot in GetDoll(dataManager.CurDollID).Equipment)
				if (slot != null && slot.Data is EquipmentData equipment)
					worn.Add(equipment);
			return worn;
		}

		private List<ShopCompareRow> CompareRowsOf(EquipmentData equipment)
		{
			List<EquipmentData> worn = WornEquipment();
			List<ShopCompareRow> rows = new();
			foreach ((SurvivalUIKind kind, string name) in COMPARE_KINDS)
			{
				int selected = MaxStackSum(equipment, kind);
				int best = 0;
				string owner = string.Empty;
				foreach (EquipmentData other in worn)
				{
					int value = MaxStackSum(other, kind);
					if (value > best)
					{
						best = value;
						owner = other.Name;
					}
				}

				int difference = selected - best;
				rows.Add(new ShopCompareRow
				{
					Kind = kind,
					KindName = name,
					Selected = selected,
					Best = best,
					BestOwner = owner,
					Difference = difference > 0 ? $"+{difference}" : difference == 0 ? "같음" : difference.ToString(),
					Tone = difference > 0 ? ShopCompareTone.Better : difference < 0 ? ShopCompareTone.Worse : ShopCompareTone.Same,
				});
			}
			return rows;
		}

		private static int MaxStackSum(EquipmentData equipment, SurvivalUIKind kind) =>
			equipment.EffectCards.Where(card => card.UIKind == kind).Sum(card => card.MaxStack);
	}
}
