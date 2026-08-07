using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 격자 A* — 「길찾기를 직접 구현했다」가 말뿐이 아닌지 못 박는다 (TASK-WM-194).
	///
	/// ★ 이 시험이 필요한 이유: 예전 안내(흐름장)는 목표가 코어가 아니면 손을 떼고 직선을 돌려줬고,
	///   그래서 마수가 벽에 박혔다. 「돌아간다 · 모서리를 안 뚫는다 · 막히면 못 간다고 말한다」
	///   이 셋은 화면 없이도 잴 수 있다 — 잴 수 있는 것을 Play 로 미루면 영영 안 재게 된다.
	/// </summary>
	public class TowerDefenseGridPathTests
	{
		private static TowerDefenseGridPath Make(int width, int length, HashSet<Vector2Int> walls)
		{
			return new TowerDefenseGridPath(width, length, cell => walls.Contains(cell));
		}

		[Test]
		public void 벽이_없으면_곧장_간다()
		{
			TowerDefenseGridPath path = Make(10, 10, new HashSet<Vector2Int>());
			List<Vector2Int> steps = new();

			Assert.IsTrue(path.Find(new Vector2Int(0, 0), new Vector2Int(5, 0), 0f, steps));
			Assert.AreEqual(new Vector2Int(5, 0), steps[steps.Count - 1]);
			Assert.AreEqual(5, steps.Count, "직선 다섯 칸이면 다섯 걸음이다.");
		}

		[Test]
		public void 가로막힌_벽은_돌아간다()
		{
			// x=3 열을 위아래로 막고 한 칸(y=9)만 터둔다 — 돌아가지 않으면 절대 못 닿는다.
			HashSet<Vector2Int> walls = new();
			for (int y = 0; y < 9; y++)
				walls.Add(new Vector2Int(3, y));

			TowerDefenseGridPath path = Make(12, 12, walls);
			List<Vector2Int> steps = new();

			Assert.IsTrue(path.Find(new Vector2Int(0, 0), new Vector2Int(6, 0), 0f, steps),
				"터진 틈으로 돌아갈 수 있어야 한다.");
			foreach (Vector2Int step in steps)
				Assert.IsFalse(walls.Contains(step), "벽 칸을 밟고 지나가면 안 된다 — 그게 「벽을 통과하는」 그림이다.");
			Assert.Greater(steps.Count, 6, "돌아간 길은 직선보다 길다.");
		}

		[Test]
		public void 완전히_둘러싸이면_못_간다고_말한다()
		{
			// 목표를 벽으로 완전히 감싼다. 예전엔 이럴 때 직선을 돌려줘서 마수가 벽으로 걸어 들어갔다.
			HashSet<Vector2Int> walls = new();
			Vector2Int goal = new(6, 6);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0)
						continue;
					walls.Add(new Vector2Int(goal.x + dx, goal.y + dy));
				}
			}

			TowerDefenseGridPath path = Make(12, 12, walls);
			List<Vector2Int> steps = new();

			Assert.IsFalse(path.Find(new Vector2Int(0, 0), goal, 0f, steps),
				"길이 없으면 false 여야 한다 — 여기서 true 를 주면 직선으로 벽을 뚫는다.");
			Assert.AreEqual(0, steps.Count);
		}

		[Test]
		public void 막힌_목표라도_그_칸까지_안내한다()
		{
			// 마수의 목표는 *부술 벽*일 때가 많다. 벽 칸이라고 포기하면 「목표는 있는데 갈 수 없다」가 된다.
			HashSet<Vector2Int> walls = new() { new Vector2Int(5, 0) };
			TowerDefenseGridPath path = Make(10, 10, walls);
			List<Vector2Int> steps = new();

			Assert.IsTrue(path.Find(new Vector2Int(0, 0), new Vector2Int(5, 0), 0f, steps));
			Assert.AreEqual(new Vector2Int(5, 0), steps[steps.Count - 1]);
		}

		[Test]
		public void 대각선으로_모서리를_뚫지_않는다()
		{
			// (1,0) 과 (0,1) 이 막혀 있으면 (0,0) → (1,1) 로 비스듬히 빠져나갈 수 없어야 한다.
			HashSet<Vector2Int> walls = new() { new Vector2Int(1, 0), new Vector2Int(0, 1) };
			TowerDefenseGridPath path = Make(6, 6, walls);
			List<Vector2Int> steps = new();

			bool found = path.Find(new Vector2Int(0, 0), new Vector2Int(1, 1), 0f, steps);
			if (found)
			{
				Assert.AreNotEqual(new Vector2Int(1, 1), steps[0],
					"첫 걸음이 곧장 대각선이면 두 벽 사이 모서리를 뚫은 것이다.");
			}
		}

		[Test]
		public void 탐색_상한이_판_크기를_따라_커진다()
		{
			// ★ 상수 상한은 판이 커질수록 조용히 모자라진다 — 그 증상은 「몇 마리가 안 움직인다」뿐이다.
			//   작은 판에서는 최소값을 지키고, 큰 판에서는 판을 따라 커져야 한다.
			TowerDefenseGridPath small = Make(20, 20, new HashSet<Vector2Int>());
			TowerDefenseGridPath big = Make(200, 200, new HashSet<Vector2Int>());

			Assert.AreEqual(4000, small.MaxExpandedCells, "작은 판은 최소값을 지킨다.");
			Assert.Greater(big.MaxExpandedCells, small.MaxExpandedCells, "큰 판인데 상한이 그대로면 길을 못 찾는다.");
			Assert.AreEqual(20000, big.MaxExpandedCells);
		}

		[Test]
		public void 같은_입력이면_같은_길이_나온다()
		{
			HashSet<Vector2Int> walls = new();
			for (int y = 0; y < 7; y++)
				walls.Add(new Vector2Int(4, y));

			TowerDefenseGridPath path = Make(12, 12, walls);
			List<Vector2Int> first = new();
			List<Vector2Int> second = new();

			Assert.IsTrue(path.Find(new Vector2Int(0, 0), new Vector2Int(8, 0), 0.5f, first));
			Assert.IsTrue(path.Find(new Vector2Int(0, 0), new Vector2Int(8, 0), 0.5f, second));
			CollectionAssert.AreEqual(first, second, "결정적이어야 한다 — 같은 판·같은 목표면 같은 길.");
		}
	}
}
