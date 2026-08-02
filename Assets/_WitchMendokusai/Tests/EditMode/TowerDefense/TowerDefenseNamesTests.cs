using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 인형 이름 회귀 — 애착은 「같은 아이가 같은 이름으로 다시 보이는 것」에서만 생긴다.
	/// 이름이 프레임마다 흔들리면 그건 이름이 아니라 잡음이다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseNamesTests
	{
		[Test]
		public void 같은_판_같은_순서면_같은_이름()
		{
			Assert.AreEqual(TowerDefenseNames.For(777, 3), TowerDefenseNames.For(777, 3));
		}

		[Test]
		public void 이름이_비어있지_않다()
		{
			for (int ordinal = 0; ordinal < 40; ordinal++)
				Assert.IsNotEmpty(TowerDefenseNames.For(12, ordinal));
		}

		[Test]
		public void 연달아_세운_인형이_같은_이름을_달지_않는다()
		{
			// 처음 몇 개가 다 같은 이름이면 「이름」이 정체가 아니라 장식이 된다.
			HashSet<string> names = new();
			for (int ordinal = 0; ordinal < 6; ordinal++)
				names.Add(TowerDefenseNames.For(2026, ordinal));

			Assert.GreaterOrEqual(names.Count, 5, "여섯 아이 중 다섯은 서로 다른 이름이어야 한다.");
		}

		[Test]
		public void 판이_다르면_이름_구성이_달라진다()
		{
			bool anyDifference = false;
			for (int ordinal = 0; ordinal < 8; ordinal++)
			{
				if (TowerDefenseNames.For(1, ordinal) != TowerDefenseNames.For(2, ordinal))
					anyDifference = true;
			}

			Assert.IsTrue(anyDifference, "판이 달라도 이름이 같으면 매 판이 같은 아이들이 된다.");
		}

		[Test]
		public void 인사말도_결정적이고_비어있지_않다()
		{
			Assert.AreEqual(TowerDefenseNames.Greeting(5, 1), TowerDefenseNames.Greeting(5, 1));
			Assert.IsNotEmpty(TowerDefenseNames.Greeting(5, 1));
		}
	}
}
