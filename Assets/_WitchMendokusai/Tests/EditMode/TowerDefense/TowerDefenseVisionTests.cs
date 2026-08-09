using System.Collections.Generic;
using NUnit.Framework;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

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

		[Test]
		public void 판이_자라도_밝힌_곳은_어두워지지_않는다()
		{
			// ★ 무한 맵의 창이 커질 때마다 시야를 새로 구우면 *가봤던 곳이 통째로 어두워진다*.
			//   화면상 「왜 다시 안 보이지」가 되는데, 규칙이 조용히 되돌린 것이라 원인이 안 보인다.
			TowerDefenseVision small = new(10, 10);
			small.Recompute(new[] { new TowerDefenseVision.Source(new Vector2Int(2, 2), 1.5f) });
			Vector2Int seen = new(2, 2);
			Assert.AreNotEqual(TowerDefenseVisionState.Unseen, small.StateAt(seen), "먼저 밝혀져 있어야 시험이 성립한다.");

			TowerDefenseVision grown = new(20, 20);
			grown.CopyExploredFrom(small);

			Assert.AreEqual(TowerDefenseVisionState.Explored, grown.StateAt(seen), "판이 커지자 밝힌 곳이 도로 어두워졌다.");
		}

		[Test]
		public void 자란_판의_새_띠는_안_가본_곳이다()
		{
			// 옮기는 것은 *기록*이지 「전부 밝힘」이 아니다 — 새로 열린 곳까지 밝히면 안개가 의미를 잃는다.
			TowerDefenseVision small = new(10, 10);
			small.Recompute(new[] { new TowerDefenseVision.Source(new Vector2Int(5, 5), 20f) });

			TowerDefenseVision grown = new(20, 20);
			grown.CopyExploredFrom(small);

			Assert.AreEqual(TowerDefenseVisionState.Unseen, grown.StateAt(new Vector2Int(15, 15)));
		}
	}
}
