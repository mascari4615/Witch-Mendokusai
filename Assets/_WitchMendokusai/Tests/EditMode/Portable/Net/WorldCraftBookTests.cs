using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 제작을 <b>세계가 판정한다</b> (TASK-WM-217).
	///
	/// ★ 왜: 지금 제작은 창 안에서 끝난다 — 재료 확인도, 성공 주사위도, 지급도 창이 한다.
	///   창을 고친 사람은 언제나 성공하고 무엇이든 만든다.
	/// </summary>
	public sealed class WorldCraftBookTests
	{
		private const int PLANK = 1;
		private const int WOOD = 0; // ⚠ 나무가 0 번이다 — 「없음」으로 걸러지면 조용히 공짜가 된다.

		private static WorldCraftBook Book(float percentage = 100f)
		{
			return new WorldCraftBook(new CraftCatalogData
			{
				recipes = new[]
				{
					new CraftRecipeEntry
					{
						id = 10, name = "나무 판자", resultItemId = PLANK, resultAmount = 2, percentage = percentage,
						items = new[] { new CraftIngredientEntry { itemId = WOOD, amount = 3 } },
					},
				},
			});
		}

		private static System.Func<int, int> Bag(params (int itemId, int amount)[] carried)
		{
			Dictionary<int, int> bag = new Dictionary<int, int>();
			for (int i = 0; i < carried.Length; i++)
				bag[carried[i].itemId] = carried[i].amount;

			return itemId => bag.TryGetValue(itemId, out int amount) ? amount : 0;
		}

		[Test]
		public void 재료가_있으면_만들어진다()
		{
			CraftResult result = Book().Judge(10, Bag((WOOD, 3)), roll: 0f);

			Assert.IsTrue(result.Succeeded);
			Assert.AreEqual(PLANK, result.ResultItemId);
			Assert.AreEqual(2, result.ResultAmount);
		}

		[Test]
		public void 재료가_모자라면_시도조차_안_한다()
		{
			CraftResult result = Book().Judge(10, Bag((WOOD, 2)), roll: 0f);

			Assert.IsFalse(result.Attempted, "재료도 없이 주사위를 굴리면 실패 보상이 공짜로 나온다");
			Assert.AreEqual("재료가 모자란다", result.Denied, "왜 안 되는지 안 알려주면 사람은 「고장」으로 읽는다");
		}

		[Test]
		public void 나무는_0번이라도_진짜_재료다()
		{
			CraftResult result = Book().Judge(10, Bag(), roll: 0f);

			Assert.IsFalse(result.Attempted, "번호 0 을 「없음」으로 거르면 나무로 만드는 게 전부 공짜가 된다");
		}

		[Test]
		public void 세계가_모르는_제작은_거절한다()
		{
			CraftResult result = Book().Judge(999, Bag((WOOD, 99)), roll: 0f);

			Assert.IsFalse(result.Attempted);
			Assert.AreEqual("세계가 모르는 제작이다", result.Denied);
		}

		[Test]
		public void 주사위를_지면_재료만_쓰고_실패한다()
		{
			CraftResult result = Book(percentage: 30f).Judge(10, Bag((WOOD, 3)), roll: 50f);

			Assert.IsTrue(result.Attempted, "재료는 들었다 — 시도는 있었다");
			Assert.IsFalse(result.Succeeded);
			Assert.AreEqual(0, result.ResultAmount, "실패했는데 물건이 나오면 실패가 아니다");
		}

		[Test]
		public void 주사위를_이기면_성공한다()
		{
			CraftResult result = Book(percentage: 30f).Judge(10, Bag((WOOD, 3)), roll: 29.9f);

			Assert.IsTrue(result.Succeeded);
		}

		[Test]
		public void 성공률이_안_적힌_옛_줄은_반드시_성공한다()
		{
			// 0 을 「절대 실패」로 읽으면 자료를 안 고친 줄이 통째로 죽는다.
			CraftResult result = Book(percentage: 0f).Judge(10, Bag((WOOD, 3)), roll: 99f);

			Assert.IsTrue(result.Succeeded);
		}

		[Test]
		public void 같은_번호가_두_번_적혀도_한_줄만_남는다()
		{
			WorldCraftBook book = new WorldCraftBook(new CraftCatalogData
			{
				recipes = new[]
				{
					new CraftRecipeEntry { id = 7, resultItemId = PLANK, resultAmount = 1 },
					new CraftRecipeEntry { id = 7, resultItemId = 999, resultAmount = 9 },
				},
			});

			Assert.AreEqual(1, book.Recipes.Count, "같은 번호가 둘이면 어느 쪽이 나올지 판마다 흔들린다");
			Assert.AreEqual(PLANK, book.Find(7).resultItemId);
		}
	}
}
