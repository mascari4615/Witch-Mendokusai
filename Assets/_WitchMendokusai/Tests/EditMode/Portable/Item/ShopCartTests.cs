using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShopCartTests
	{
		private const int LINE_LIMIT = 99;

		private sealed class FakeBag : IShopBag
		{
			public readonly Dictionary<int, int> Counts = new();
			public int Capacity = int.MaxValue;

			public int Consume(int itemId, int amount)
			{
				Counts.TryGetValue(itemId, out int owned);
				int taken = Math.Min(owned, amount);
				Counts[itemId] = owned - taken;
				return amount - taken;
			}

			public int Add(int itemId, int amount)
			{
				int placed = Math.Min(Capacity, amount);
				Capacity -= placed;
				Counts.TryGetValue(itemId, out int owned);
				Counts[itemId] = owned + placed;
				return amount - placed;
			}
		}

		private static ShopGoods Flask(int owned = 0, int buyPrice = 5) => new(1, buyPrice, 2, 9999, owned);
		private static ShopGoods Weapon(int owned = 0, int buyPrice = 50) => new(2, buyPrice, 20, 1, owned);
		private static ShopGoods Wood(int owned = 12) => new(3, 2, 1, 500, owned);

		[Test]
		public void NetSumsBuyAndSellLines()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Weapon(), ShopCartSide.Buy, 240);
			cart.AddOne(Wood(), ShopCartSide.Sell, 240);
			cart.AddOne(Wood(), ShopCartSide.Sell, 240);

			Assert.AreEqual(-58, cart.Net);
			Assert.AreEqual(182, cart.WalletAfter(240));
			Assert.IsTrue(cart.CanCheckout(240));
		}

		[Test]
		public void AddingDoesNotTouchWallet()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Weapon(), ShopCartSide.Buy, 240);
			Assert.AreEqual(240, cart.WalletAfter(240) + 50);
		}

		[Test]
		public void CannotAddItemPricedAboveWallet()
		{
			ShopCart cart = new(LINE_LIMIT);
			Assert.AreEqual(ShopCartAddResult.CannotAfford, cart.AddOne(Weapon(), ShopCartSide.Buy, 30));
			Assert.IsTrue(cart.IsEmpty);
		}

		[Test]
		public void TotalAboveWalletBlocksCheckoutAndReportsShortfall()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(new ShopGoods(10, 20, 1, 500, 0), ShopCartSide.Buy, 30);
			cart.AddOne(new ShopGoods(10, 20, 1, 500, 0), ShopCartSide.Buy, 30);

			Assert.AreEqual(10, cart.Shortfall(30));
			Assert.IsFalse(cart.CanCheckout(30));

			FakeBag bag = new();
			ShopCheckoutResult result = cart.Checkout(30, bag);
			Assert.IsFalse(result.Succeeded);
			Assert.AreEqual(30, result.Wallet);
			Assert.AreEqual(2, cart.Lines[0].Amount);
			Assert.AreEqual(0, bag.Counts.Count);
		}

		[Test]
		public void SellLinesCanFundBuyLines()
		{
			ShopCart cart = new(LINE_LIMIT);
			ShopGoods expensive = new(10, 35, 1, 500, 0);
			Assert.AreEqual(ShopCartAddResult.Added, cart.AddOne(new ShopGoods(10, 30, 1, 500, 0), ShopCartSide.Buy, 30));
			cart.AddOne(new ShopGoods(10, 30, 1, 500, 0), ShopCartSide.Buy, 30);
			for (int i = 0; i < 30; i++)
				cart.AddOne(new ShopGoods(3, 2, 1, 500, 30), ShopCartSide.Sell, 30);

			Assert.AreEqual(ShopCartAddResult.CannotAfford, cart.AddOne(expensive, ShopCartSide.Buy, 30));
			Assert.IsTrue(cart.CanCheckout(30));
			Assert.AreEqual(0, cart.WalletAfter(30));
		}

		[Test]
		public void SingleItemBuyLimitIgnoresOwned()
		{
			ShopCart cart = new(LINE_LIMIT);
			ShopGoods owned = Weapon(owned: 1);
			Assert.AreEqual(ShopCartAddResult.Added, cart.AddOne(owned, ShopCartSide.Buy, 240));
			Assert.AreEqual(ShopCartAddResult.BuyLimitReached, cart.AddOne(owned, ShopCartSide.Buy, 240));
			Assert.AreEqual(1, cart.AmountOf(owned.ItemId, ShopCartSide.Buy));
		}

		[Test]
		public void CountableBuyStopsAtRemainingRoom()
		{
			ShopCart cart = new(LINE_LIMIT);
			ShopGoods goods = new(3, 1, 1, 500, 498);
			Assert.AreEqual(2, cart.LimitOf(goods, ShopCartSide.Buy));
			cart.AddOne(goods, ShopCartSide.Buy, 240);
			cart.AddOne(goods, ShopCartSide.Buy, 240);
			Assert.AreEqual(ShopCartAddResult.BuyLimitReached, cart.AddOne(goods, ShopCartSide.Buy, 240));
		}

		[Test]
		public void CountableBuyStopsAtLineLimit()
		{
			ShopCart cart = new(3);
			ShopGoods goods = new(1, 0, 2, 9999, 0);
			for (int i = 0; i < 3; i++)
				Assert.AreEqual(ShopCartAddResult.Added, cart.AddOne(goods, ShopCartSide.Buy, 0));
			Assert.AreEqual(ShopCartAddResult.BuyLimitReached, cart.AddOne(goods, ShopCartSide.Buy, 0));
		}

		[Test]
		public void SellStopsAtOwned()
		{
			ShopCart cart = new(LINE_LIMIT);
			ShopGoods goods = Wood(owned: 1);
			Assert.AreEqual(ShopCartAddResult.Added, cart.AddOne(goods, ShopCartSide.Sell, 0));
			Assert.AreEqual(ShopCartAddResult.SellLimitReached, cart.AddOne(goods, ShopCartSide.Sell, 0));
		}

		[Test]
		public void FreeShopChargesNothingButStillPaysSales()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Weapon(buyPrice: 0), ShopCartSide.Buy, 0);
			cart.AddOne(Wood(), ShopCartSide.Sell, 0);

			FakeBag bag = new();
			bag.Counts[3] = 12;
			ShopCheckoutResult result = cart.Checkout(0, bag);
			Assert.IsTrue(result.Succeeded);
			Assert.AreEqual(1, result.Wallet);
			Assert.AreEqual(1, bag.Counts[2]);
			Assert.AreEqual(11, bag.Counts[3]);
		}

		[Test]
		public void BuyAndSellOfSameItemAreSeparateLines()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Flask(owned: 6), ShopCartSide.Buy, 240);
			cart.AddOne(Flask(owned: 6), ShopCartSide.Sell, 240);
			Assert.AreEqual(2, cart.Lines.Count);
			Assert.AreEqual(-3, cart.Net);
		}

		[Test]
		public void RemoveOneDropsLineAtZero()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);

			Assert.IsTrue(cart.RemoveOne(1, ShopCartSide.Buy));
			Assert.AreEqual(1, cart.AmountOf(1, ShopCartSide.Buy));
			Assert.IsTrue(cart.RemoveOne(1, ShopCartSide.Buy));
			Assert.IsTrue(cart.IsEmpty);
			Assert.IsFalse(cart.RemoveOne(1, ShopCartSide.Buy));
		}

		[Test]
		public void EmptyCartCannotCheckout()
		{
			ShopCart cart = new(LINE_LIMIT);
			Assert.IsFalse(cart.CanCheckout(240));
			Assert.IsFalse(cart.Checkout(240, new FakeBag()).Succeeded);
		}

		[Test]
		public void CheckoutAppliesWalletAndBagThenClearsCart()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Weapon(), ShopCartSide.Buy, 240);
			cart.AddOne(Wood(), ShopCartSide.Sell, 240);

			FakeBag bag = new();
			bag.Counts[3] = 12;
			ShopCheckoutResult result = cart.Checkout(240, bag);

			Assert.IsTrue(result.Succeeded);
			Assert.AreEqual(181, result.Wallet);
			Assert.AreEqual(-59, result.Net);
			Assert.AreEqual(3, result.BoughtAmount);
			Assert.AreEqual(1, result.SoldAmount);
			Assert.AreEqual(2, bag.Counts[1]);
			Assert.AreEqual(1, bag.Counts[2]);
			Assert.AreEqual(11, bag.Counts[3]);
			Assert.IsTrue(cart.IsEmpty);
		}

		[Test]
		public void FullBagChargesOnlyPlacedItems()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);
			cart.AddOne(Flask(), ShopCartSide.Buy, 240);

			FakeBag bag = new() { Capacity = 1 };
			ShopCheckoutResult result = cart.Checkout(240, bag);

			Assert.AreEqual(235, result.Wallet);
			Assert.AreEqual(1, result.BoughtAmount);
			Assert.AreEqual(2, result.UnplacedAmount);
		}

		[Test]
		public void MissingSaleCannotOverdraftBuys()
		{
			ShopCart cart = new(LINE_LIMIT);
			cart.AddOne(new ShopGoods(10, 30, 1, 500, 0), ShopCartSide.Buy, 30);
			cart.AddOne(new ShopGoods(10, 30, 1, 500, 0), ShopCartSide.Buy, 30);
			for (int i = 0; i < 30; i++)
				cart.AddOne(new ShopGoods(3, 2, 1, 500, 30), ShopCartSide.Sell, 30);

			FakeBag bag = new();
			bag.Counts[3] = 0;
			ShopCheckoutResult result = cart.Checkout(30, bag);

			Assert.AreEqual(0, result.Wallet);
			Assert.AreEqual(0, result.SoldAmount);
			Assert.AreEqual(1, result.BoughtAmount);
			Assert.AreEqual(1, result.UnplacedAmount);
		}

		[Test]
		public void ResaleProfitPriceIsReportedAsIs()
		{
			ShopCart cart = new(LINE_LIMIT);
			ShopGoods goldOre = new(28, 4, 6, 500, 5);
			cart.AddOne(goldOre, ShopCartSide.Buy, 240);
			cart.AddOne(goldOre, ShopCartSide.Sell, 240);
			Assert.AreEqual(2, cart.Net);
		}
	}
}
