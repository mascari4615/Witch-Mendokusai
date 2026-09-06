using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 화면이 그리는 목표 = <b>세계의 마도서</b> (TASK-WM-217).
	///
	/// ★ 왜: 보상은 세계가 정하는데 화면은 자기 자산으로 목표를 그렸다. 둘이 어긋나면
	///   사람은 표시대로 저은 뒤 딴 것을 받는다 — 화면은 「최상급」, 세계는 「조잡」도 가능하다.
	/// </summary>
	public sealed class WorldSpellbookViewTests
	{
		private static RecipeCatalogEntry Page(int id, float x, float y, float radius = 0.8f)
		{
			return new RecipeCatalogEntry
			{
				id = id, name = "쪽" + id, targetX = x, targetY = y, radius = radius, resultItemId = id, amount = 1,
			};
		}

		private static BrewState StirredTo(float x, float y)
		{
			return new BrewState { Position = new BrewVector(x, y), StepCount = 1 };
		}

		[Test]
		public void 마도서_한_쪽이_화면_목표가_된다()
		{
			BrewRecipe recipe = WorldSpellbookView.ToRecipe(Page(3, 2f, -1f, 0.5f));

			Assert.AreEqual(3, recipe.Id);
			Assert.AreEqual(2f, recipe.Target.Position.X, 0.0001f);
			Assert.AreEqual(-1f, recipe.Target.Position.Y, 0.0001f);
			Assert.AreEqual(0.5f, recipe.Target.Radius, 0.0001f, "반경이 다르면 「닿았다」 표시가 세계와 어긋난다");
		}

		[Test]
		public void 반경을_안_적은_쪽도_그릴_수_있다()
		{
			BrewRecipe recipe = WorldSpellbookView.ToRecipe(Page(1, 0f, 0f, 0f));

			Assert.Greater(recipe.Target.Radius, 0f, "반경 0 이면 영원히 「못 닿았다」로 보인다");
		}

		[Test]
		public void 지금_노리는_쪽은_가장_가까운_쪽이다()
		{
			RecipeCatalogEntry[] pages = { Page(1, 0f, 0f), Page(2, 2f, 0f), Page(3, 0f, 2f) };

			Assert.IsTrue(WorldSpellbookView.TryAim(pages, StirredTo(1.6f, 0f), out BrewRecipe aimed));
			Assert.AreEqual(2, aimed.Id, "먼 쪽을 그리면 사람이 조준한 곳과 화면이 다르다");
		}

		[Test]
		public void 아직_안_저었으면_첫_쪽을_그린다()
		{
			RecipeCatalogEntry[] pages = { Page(1, 0f, 0f), Page(2, 5f, 5f) };

			Assert.IsTrue(WorldSpellbookView.TryAim(pages, BrewState.Start, out BrewRecipe aimed));
			Assert.AreEqual(1, aimed.Id);
		}

		[Test]
		public void 같은_거리면_먼저_적힌_쪽으로_고정된다()
		{
			// 흔들리면 사람은 조준을 못 한다 — 프레임마다 목표가 바뀌는 화면이 된다.
			RecipeCatalogEntry[] pages = { Page(7, 1f, 0f), Page(8, -1f, 0f) };

			Assert.IsTrue(WorldSpellbookView.TryAim(pages, BrewState.Start, out BrewRecipe aimed));
			Assert.AreEqual(7, aimed.Id);
		}

		[Test]
		public void 마도서가_비면_그릴_것이_없다고_말한다()
		{
			Assert.IsFalse(WorldSpellbookView.TryAim(new RecipeCatalogEntry[0], BrewState.Start, out BrewRecipe _),
				"빈 마도서를 「목표 (0,0)」으로 지어내면 아무 데나 저어도 닿은 것처럼 보인다");
			Assert.IsFalse(WorldSpellbookView.TryAim(null, BrewState.Start, out BrewRecipe _));
		}
	}
}
