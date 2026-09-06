using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
	/// <summary>넣은 재료가 솥을 민다 — 방향은 세계가 안다 (TASK-WM-217).</summary>
	public sealed class WorldIngredientsTests
	{
		private const int WOOD = 0;
		private const int COAL = 4;

		private static WorldIngredients Shelf()
		{
			return new WorldIngredients(new IngredientCatalogData
			{
				ingredients = new[]
				{
					new IngredientCatalogEntry { itemId = WOOD, name = "나무", dx = 1f, dy = 0f, grind = 0.5f },
					new IngredientCatalogEntry { itemId = COAL, name = "석탄", dx = 0f, dy = 1f, grind = 1f },
				},
			});
		}

		[Test]
		public void 재료가_어느_쪽으로_미는지_안다()
		{
			Assert.IsTrue(Shelf().TryStep(WOOD, out BrewStep step));
			Assert.AreEqual(1f, step.Direction.X, 0.0001f);
			Assert.AreEqual(0.5f, step.Grind, 0.0001f);
		}

		[Test]
		public void 재료가_아닌_것은_못_넣는다()
		{
			Assert.IsFalse(Shelf().TryStep(99999, out _), "아무거나 넣어지면 솥은 규칙이 아니다");
		}

		[Test]
		public void 넣은_재료대로_솥이_움직인다()
		{
			WorldCauldron cauldron = new WorldCauldron();
			WorldIngredients shelf = Shelf();

			shelf.TryStep(WOOD, out BrewStep wood);
			cauldron.AddStep(wood);
			shelf.TryStep(COAL, out BrewStep coal);
			cauldron.AddStep(coal);

			// 나무(오른쪽 0.5) + 석탄(위 1) — 창이 정한 게 아니라 재료가 정한 자리다.
			Assert.AreEqual(0.5f, cauldron.State.Position.X, 0.0001f);
			Assert.AreEqual(1f, cauldron.State.Position.Y, 0.0001f);
			Assert.AreEqual(2, cauldron.State.StepCount);
		}

		[Test]
		public void 빈_선반이면_아무것도_못_넣는다()
		{
			WorldIngredients empty = new WorldIngredients(null);

			Assert.AreEqual(0, empty.Count);
			Assert.IsFalse(empty.TryStep(WOOD, out _));
		}
	}
}
