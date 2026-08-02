using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 시야 규칙 회귀 — 3단계(안 가봤음/기억함/보임). 전투가 이 판정에 걸려 있으므로
	/// 여기가 틀리면 「포탑이 왜 안 쏘지」가 버그로 읽힌다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseVisionTests
	{
		private const int WIDTH = 20;
		private const int LENGTH = 20;

		private static TowerDefenseVision Vision() => new TowerDefenseVision(WIDTH, LENGTH);

		private static List<TowerDefenseVision.Source> One(int x, int y, float radius)
		{
			return new List<TowerDefenseVision.Source> { new(new Vector2Int(x, y), radius) };
		}

		[Test]
		public void 처음엔_전부_안_가본_상태()
		{
			TowerDefenseVision vision = Vision();

			Assert.AreEqual(TowerDefenseVisionState.Unseen, vision.StateAt(new Vector2Int(0, 0)));
			Assert.AreEqual(TowerDefenseVisionState.Unseen, vision.StateAt(new Vector2Int(10, 10)));
		}

		[Test]
		public void 반경_안은_보이고_밖은_안_보인다()
		{
			TowerDefenseVision vision = Vision();
			vision.Recompute(One(10, 10, 3f));

			Assert.IsTrue(vision.IsVisible(new Vector2Int(10, 12)));
			Assert.IsFalse(vision.IsVisible(new Vector2Int(10, 15)));
		}

		[Test]
		public void 밝힌_곳은_시야가_빠져도_기억한다()
		{
			// 한 번 안 것을 잊는 게임은 억울하다 — Explored 는 되돌아가지 않는다.
			TowerDefenseVision vision = Vision();
			vision.Recompute(One(10, 10, 3f));
			vision.Recompute(One(2, 2, 1f)); // 시야원이 딴 데로 옮겨감.

			Assert.IsFalse(vision.IsVisible(new Vector2Int(10, 10)));
			Assert.IsTrue(vision.IsExplored(new Vector2Int(10, 10)));
			Assert.AreEqual(TowerDefenseVisionState.Explored, vision.StateAt(new Vector2Int(10, 10)));
		}

		[Test]
		public void 시야원이_늘면_보이는_범위가_넓어진다()
		{
			TowerDefenseVision vision = Vision();
			vision.Recompute(One(4, 4, 2f));
			Assert.IsFalse(vision.IsVisible(new Vector2Int(14, 14)));

			vision.Recompute(new List<TowerDefenseVision.Source>
			{
				new(new Vector2Int(4, 4), 2f),
				new(new Vector2Int(14, 14), 2f),
			});

			Assert.IsTrue(vision.IsVisible(new Vector2Int(14, 14)), "개척(건물 확장) = 시야 확장이어야 한다.");
			Assert.IsTrue(vision.IsVisible(new Vector2Int(4, 4)));
		}

		[Test]
		public void 판_밖은_안_보이는_것으로_본다()
		{
			TowerDefenseVision vision = Vision();
			vision.Recompute(One(0, 0, 5f));

			Assert.IsFalse(vision.IsVisible(new Vector2Int(-1, 0)));
			Assert.IsFalse(vision.IsVisible(new Vector2Int(WIDTH, 0)));
			Assert.AreEqual(TowerDefenseVisionState.Unseen, vision.StateAt(new Vector2Int(-1, -1)));
		}

		[Test]
		public void 반경0_시야원은_아무것도_밝히지_않는다()
		{
			TowerDefenseVision vision = Vision();
			vision.Recompute(One(10, 10, 0f));

			Assert.IsFalse(vision.IsVisible(new Vector2Int(10, 10)));
		}
	}
}
