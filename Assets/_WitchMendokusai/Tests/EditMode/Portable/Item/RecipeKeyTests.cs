using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 제조법 열쇠가 <b>엔진 없이도</b> 같은 문자열을 낸다 (TASK-WM-215).
	/// 이 규칙이 갈리면 같은 재료를 넣고도 한쪽에서만 물건이 나온다.
	/// </summary>
	public sealed class RecipeKeyTests
	{
		[Test]
		public void 재료_순서가_달라도_같은_열쇠가_나온다()
		{
			string first = RecipeKey.Build(RecipeType.ItemCraft, new List<int> { 30, 10, 20 });
			string second = RecipeKey.Build(RecipeType.ItemCraft, new List<int> { 10, 20, 30 });

			Assert.AreEqual(first, second, "넣은 순서는 제조 결과를 바꾸지 않는다");
		}

		[Test]
		public void 종류가_다르면_열쇠도_다르다()
		{
			string cauldron = RecipeKey.Build(RecipeType.ItemCraft, new List<int> { 1, 2 });
			string other = RecipeKey.Build(RecipeType.Smelting, new List<int> { 1, 2 });

			Assert.AreNotEqual(cauldron, other);
		}

		[Test]
		public void 원본_목록은_건드리지_않는다()
		{
			List<int> ingredients = new List<int> { 3, 1, 2 };

			RecipeKey.Build(RecipeType.ItemCraft, ingredients);

			Assert.AreEqual(3, ingredients[0], "부른 쪽 목록이 몰래 정렬되면 안 된다");
		}

		[Test]
		public void 같은_재료가_여러_개면_그_개수까지_센다()
		{
			string twoStones = RecipeKey.Build(RecipeType.ItemCraft, new List<int> { 5, 5 });
			string oneStone = RecipeKey.Build(RecipeType.ItemCraft, new List<int> { 5 });

			Assert.AreNotEqual(twoStones, oneStone, "돌 두 개와 한 개는 다른 제조법이다");
		}

		[Test]
		public void 재료가_없어도_터지지_않는다()
		{
			string empty = RecipeKey.Build(RecipeType.ItemCraft, null);

			Assert.IsNotNull(empty);
			Assert.IsTrue(empty.EndsWith(","), "재료 칸이 비어 있을 뿐");
		}
	}
}
