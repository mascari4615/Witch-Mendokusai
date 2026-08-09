using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
	/// <summary>완성이 무엇을 주는지는 <b>세계</b>가 정한다 (TASK-WM-217).</summary>
	public sealed class WorldRecipeBookTests
	{
		private static RecipeCatalogData Book(params RecipeCatalogEntry[] pages)
		{
			return new RecipeCatalogData { recipes = pages };
		}

		private static BrewState At(float x, float y, int steps, float side = 0f)
		{
			return new BrewState
			{
				Position = new BrewVector(x, y),
				StepCount = steps,
				AccruedSideEffect = side,
			};
		}

		[Test]
		public void 닿은_쪽의_물건이_나온다()
		{
			WorldRecipeBook book = new WorldRecipeBook(Book(new RecipeCatalogEntry
			{
				id = 1, name = "치유", targetX = 1f, targetY = 0f, radius = 0.5f, resultItemId = 17450, amount = 1,
			}));

			BrewCompletion result = book.Judge(At(1f, 0f, 3));

			Assert.AreEqual(17450, result.ResultItemId);
			Assert.AreEqual(1, result.RecipeId);
			Assert.IsFalse(result.Empty);
		}

		[Test]
		public void 아무_쪽에도_못_닿으면_빈손이다()
		{
			WorldRecipeBook book = new WorldRecipeBook(Book(new RecipeCatalogEntry
			{
				id = 1, targetX = 5f, targetY = 5f, radius = 0.2f, resultItemId = 17450,
			}));

			BrewCompletion result = book.Judge(At(0f, 0f, 3));

			Assert.IsTrue(result.Empty, "닿지도 않았는데 물건이 나오면 그건 판정이 아니다");
			Assert.AreEqual(BrewGrade.Failed, result.Grade);
		}

		[Test]
		public void 둘_다_닿으면_더_잘_저은_쪽을_준다()
		{
			// 같은 자리에서 둘 다 닿지만, 중심에 더 가까운 쪽이 품질이 높다.
			WorldRecipeBook book = new WorldRecipeBook(Book(
				new RecipeCatalogEntry { id = 1, targetX = 0.9f, targetY = 0f, radius = 2f, resultItemId = 100 },
				new RecipeCatalogEntry { id = 2, targetX = 1f, targetY = 0f, radius = 2f, resultItemId = 200 }));

			BrewCompletion result = book.Judge(At(1f, 0f, 4));

			Assert.AreEqual(200, result.ResultItemId, "중심에 더 가까운 쪽이 이긴다");
		}

		[Test]
		public void 명품이면_하나_더_준다()
		{
			WorldRecipeBook book = new WorldRecipeBook(Book(new RecipeCatalogEntry
			{
				id = 1, targetX = 0f, targetY = 0f, radius = 1f, resultItemId = 7, amount = 2,
			}));

			// 정확히 중심 + 부작용 0 = 명품.
			BrewCompletion masterwork = book.Judge(At(0f, 0f, 5));
			Assert.AreEqual(BrewGrade.Masterwork, masterwork.Grade);
			Assert.AreEqual(3, masterwork.Amount, "잘 저은 보람이 손에 남아야 한다");
		}

		[Test]
		public void 결과가_나무여도_빈_결과가_아니다()
		{
			// ⚠ 게임의 나무는 <b>0번</b>이다 — 아이템 번호로 「없음」을 판단하면 이 레시피가 통째로 사라진다.
			WorldRecipeBook book = new WorldRecipeBook(Book(new RecipeCatalogEntry
			{
				id = 9, name = "장작", targetX = 0f, targetY = 0f, radius = 1f, resultItemId = 0, amount = 1,
			}));

			BrewCompletion result = book.Judge(At(0f, 0f, 3));

			Assert.IsFalse(result.Empty, "0번 아이템도 진짜 결과다");
			Assert.AreEqual(9, result.RecipeId);
		}

		[Test]
		public void 빈_마도서면_아무것도_안_나온다()
		{
			WorldRecipeBook book = new WorldRecipeBook(new RecipeCatalogData());

			Assert.AreEqual(0, book.Count);
			Assert.IsTrue(book.Judge(At(0f, 0f, 3)).Empty);
		}

		[Test]
		public void 완성하면_솥은_비고_결과가_따라_나온다()
		{
			WorldCauldron cauldron = new WorldCauldron();
			WorldRecipeBook book = new WorldRecipeBook(Book(new RecipeCatalogEntry
			{
				id = 1, targetX = 0f, targetY = 0f, radius = 5f, resultItemId = 42,
			}));

			cauldron.AddStep(new BrewStep { Direction = new BrewVector(0.1f, 0f), Grind = 1f });

			Assert.IsTrue(cauldron.TryComplete(book, out BrewCompletion taken));
			Assert.AreEqual(42, taken.ResultItemId);
			Assert.AreEqual(0, cauldron.State.StepCount, "완성한 솥은 비어 있어야 한다");

			// 같은 순간 뒤에 온 사람은 빈 솥이다 — 두 번 주지 않는다.
			Assert.IsFalse(cauldron.TryComplete(book, out BrewCompletion second));
			Assert.IsTrue(second.Empty);
		}
	}
}
