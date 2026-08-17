using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Act;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 거둔 것의 두 갈래길 중 「파는 쪽」. 팔기는 <b>한 행동</b>이라
	/// 물건이 나가고 돈이 드는 것이 전부 되거나 전부 안 된다.
	/// </summary>
	public sealed class TradeActsTest
	{
		private const int CROP_ID = 90000900;
		private const int SALE_PRICE = 50;
		private const int PURCHASE_PRICE = 120;

		private sealed class Bag : IActResourcePool
		{
			private readonly Dictionary<int, int> amountById = new();

			public Bag(params (int Id, int Amount)[] initial)
			{
				foreach ((int id, int amount) in initial)
				{
					amountById[id] = amount;
				}
			}

			public int AmountOf(ResourceId resource) => amountById.TryGetValue(resource.Value, out int amount) ? amount : 0;

			public void Add(ResourceId resource, int amount) => amountById[resource.Value] = AmountOf(resource) + amount;
		}

		private static ItemData NewCrop()
		{
			ItemData item = ScriptableObject.CreateInstance<ItemData>();
			item.ID = CROP_ID;
			item.EditorSetPrices(PURCHASE_PRICE, SALE_PRICE);
			return item;
		}

		private static ActContext WorldWith(IActResourcePool pool) => new(null, null, pool, null, null);

		[Test]
		public void Selling_TakesTheCrop_AndPaysTheListedPrice()
		{
			ItemData crop = NewCrop();
			Bag bag = new((CROP_ID, 10), (WalletActPool.NYANG.Value, 0));

			bool sold = ActLedger.TryApply(TradeActs.Sell(crop, 3), WorldWith(bag), out _);

			Assert.That(sold, Is.True);
			Assert.That(bag.AmountOf(new ResourceId(CROP_ID)), Is.EqualTo(7), "판 만큼 물건이 나간다");
			Assert.That(bag.AmountOf(WalletActPool.NYANG), Is.EqualTo(SALE_PRICE * 3), "값은 아이템이 든 판매가 그대로");
		}

		[Test]
		public void CannotSell_WhatYouDoNotHave_AndNothingMoves()
		{
			ItemData crop = NewCrop();
			Bag bag = new((CROP_ID, 2), (WalletActPool.NYANG.Value, 100));

			bool sold = ActLedger.TryApply(TradeActs.Sell(crop, 5), WorldWith(bag), out ActOutcome outcome);

			Assert.That(sold, Is.False);
			Assert.That(outcome.Rejection, Is.EqualTo(ActRejection.Resource));
			Assert.That(bag.AmountOf(new ResourceId(CROP_ID)), Is.EqualTo(2), "거절이면 물건 그대로");
			Assert.That(bag.AmountOf(WalletActPool.NYANG), Is.EqualTo(100), "거절이면 돈도 그대로");
		}

		[Test]
		public void CannotBuy_WithoutMoney()
		{
			ItemData crop = NewCrop();
			Bag bag = new((WalletActPool.NYANG.Value, PURCHASE_PRICE - 1));

			bool bought = ActLedger.TryApply(TradeActs.Buy(crop, 1), WorldWith(bag), out ActOutcome outcome);

			Assert.That(bought, Is.False);
			Assert.That(outcome.RejectedResource, Is.EqualTo(WalletActPool.NYANG));
			Assert.That(bag.AmountOf(new ResourceId(CROP_ID)), Is.EqualTo(0), "돈이 모자라면 물건도 안 들어온다");
		}

		[Test]
		public void MoneyAndBag_LiveApart_ButMoveTogether()
		{
			// 돈은 마을 장부에, 물건은 가방에 — 그래도 팔기는 한 행동이라 함께 움직인다.
			Bag purse = new((WalletActPool.NYANG.Value, 0));
			Bag satchel = new((CROP_ID, 4));
			ActResourcePools storage = new ActResourcePools()
				.Route(WalletActPool.Handles, purse)
				.Route(resource => true, satchel);

			ItemData crop = NewCrop();
			ActLedger.TryApply(TradeActs.Sell(crop, 4), WorldWith(storage), out _);

			Assert.That(satchel.AmountOf(new ResourceId(CROP_ID)), Is.EqualTo(0));
			Assert.That(purse.AmountOf(WalletActPool.NYANG), Is.EqualTo(SALE_PRICE * 4));
		}

		[Test]
		public void UnknownResource_GoesNowhere_NotIntoTheWrongPocket()
		{
			// 어느 창고도 안 맡는 자원은 조용히 지갑에 들어가지 않는다.
			Bag purse = new((WalletActPool.NYANG.Value, 10));
			ActResourcePools storage = new ActResourcePools().Route(WalletActPool.Handles, purse);

			storage.Add(new ResourceId(CROP_ID), 5);

			Assert.That(storage.AmountOf(new ResourceId(CROP_ID)), Is.EqualTo(0));
			Assert.That(purse.AmountOf(WalletActPool.NYANG), Is.EqualTo(10), "지갑은 그대로");
		}
	}
}
