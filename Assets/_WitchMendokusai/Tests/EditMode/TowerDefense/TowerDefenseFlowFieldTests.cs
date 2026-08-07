using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 길 안내판 회귀 — 지형이 생기는 순간 「마수가 벽에 박혀 웨이브가 안 끝난다」가 되돌아온다.
	/// 그 사고를 막는 유일한 장치라 불변식을 못으로 박는다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseFlowFieldTests
	{
		private const int WIDTH = 12;
		private const int LENGTH = 12;
		private static readonly Vector2Int Goal = new Vector2Int(6, 6);

		// 세로 벽 하나 + 가운데 한 칸 뚫림 = 길목이 하나뿐인 판.
		private static bool WallWithGap(Vector2Int cell)
		{
			return cell.x == 3 && cell.y != 6;
		}

		[Test]
		public void 목표칸은_거리0()
		{
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, _ => false);

			Assert.AreEqual(0, field.DistanceTo(Goal));
		}

		[Test]
		public void 벽뒤에서도_길목을_통해_닿는다()
		{
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, WallWithGap);
			Vector2Int behindWall = new Vector2Int(0, 0);

			Assert.IsTrue(field.IsReachable(behindWall), "길목이 있는데 못 간다고 하면 마수가 벽 앞에서 굳는다.");
		}

		[Test]
		public void 화살표를_따라가면_실제로_목표에_도착한다()
		{
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, WallWithGap);
			Vector2Int current = new Vector2Int(0, 1);
			HashSet<Vector2Int> visited = new();

			for (int step = 0; step < WIDTH * LENGTH; step++)
			{
				if (current == Goal)
					break;

				Assert.IsTrue(visited.Add(current), $"같은 칸을 두 번 밟음(순환) — {current}");
				Assert.IsTrue(field.TryGetNextCell(current, out Vector2Int next), $"{current} 에서 안내 끊김");
				Assert.IsFalse(WallWithGap(next), $"벽 칸을 밟으라고 안내함 — {next}");
				current = next;
			}

			Assert.AreEqual(Goal, current, "화살표를 끝까지 따라갔는데 목표에 도착 못 함.");
		}

		[Test]
		public void 완전히_막힌_칸은_도달불가로_보고한다()
		{
			// 목표를 벽으로 완전히 두른 판 — "갈 수 있다"고 거짓말하면 마수가 영원히 벽을 민다.
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal,
				cell => Mathf.Abs(cell.x - Goal.x) == 1 && Mathf.Abs(cell.y - Goal.y) <= 1
					|| Mathf.Abs(cell.y - Goal.y) == 1 && Mathf.Abs(cell.x - Goal.x) <= 1);

			Assert.IsFalse(field.IsReachable(new Vector2Int(0, 0)));
			Assert.AreEqual(-1, field.DistanceTo(new Vector2Int(0, 0)));
		}

		/// <summary>
		/// 한쪽만 벽이어도 대각선으로 스쳐 지나가라고 안내하지 않는다.
		///
		/// ★ 왜 못으로 박나 — 마수의 몸 지름은 한 칸과 똑같다(실측: 반경 0.50 · 칸 1.00).
		///   벽 모서리와 대각선 사이에 남는 틈은 0.13 뿐이라 몸이 절대 못 들어간다. 그런데 예전 규칙은
		///   「양옆이 *둘 다* 벽일 때만」 막아서, 한쪽만 벽인 대각선을 정답이라고 알려줬다 —
		///   마수는 그 자리에서 영원히 바위를 밀었다(라이브 경고 243줄 중 54줄이 이것이었다).
		/// </summary>
		[Test]
		public void 한쪽만_벽이어도_대각선으로_모서리를_스치지_않는다()
		{
			// 목표 바로 왼쪽 한 칸만 벽. 그 아래칸에서 목표로 가는 대각선은 그 벽 모서리를 스친다.
			Vector2Int wall = new Vector2Int(Goal.x - 1, Goal.y);
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, cell => cell == wall);
			Vector2Int corner = new Vector2Int(Goal.x - 1, Goal.y - 1);

			Assert.IsTrue(field.TryGetNextCell(corner, out Vector2Int next), "안내 자체가 끊기면 안 된다 — 돌아가는 길은 있다.");
			Assert.AreNotEqual(Goal, next,
				"한쪽이 벽인데 대각선으로 질러가라고 안내했다 — 몸이 칸만큼 굵어서 그 틈으로는 못 들어간다.");
		}

		[Test]
		public void 벽칸_자체는_도달불가()
		{
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, WallWithGap);

			Assert.IsFalse(field.IsReachable(new Vector2Int(3, 0)));
		}

		[Test]
		public void 판밖은_도달불가()
		{
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, _ => false);

			Assert.IsFalse(field.IsReachable(new Vector2Int(-1, 0)));
			Assert.IsFalse(field.IsReachable(new Vector2Int(WIDTH, 0)));
		}

		[Test]
		public void 빈판에서_거리는_체비쇼프거리와_같다()
		{
			// 대각선 허용 = 8방향 → 걸음 수는 max(|dx|,|dy|).
			TowerDefenseFlowField field = new(WIDTH, LENGTH, Goal, _ => false);
			Vector2Int cell = new Vector2Int(0, 3);

			int expected = Mathf.Max(Mathf.Abs(cell.x - Goal.x), Mathf.Abs(cell.y - Goal.y));
			Assert.AreEqual(expected, field.DistanceTo(cell));
		}

		[Test]
		public void 목표가_여럿이면_가까운_쪽으로_흐른다()
		{
			// 전초기지 = 또 하나의 목표. 각 칸은 가장 가까운 목표로 흘러 마수가 저절로 분산된다.
			Vector2Int coreGoal = new Vector2Int(1, 1);
			Vector2Int outpostGoal = new Vector2Int(10, 10);
			TowerDefenseFlowField field = new(WIDTH, LENGTH, new[] { coreGoal, outpostGoal }, _ => false);

			Assert.AreEqual(0, field.DistanceTo(coreGoal));
			Assert.AreEqual(0, field.DistanceTo(outpostGoal), "두 번째 목표도 시작점이어야 한다.");
			Assert.AreEqual(1, field.DistanceTo(new Vector2Int(9, 10)), "전초기지 옆은 전초기지로 흐른다.");
			Assert.AreEqual(1, field.DistanceTo(new Vector2Int(2, 1)), "코어 옆은 코어로 흐른다.");
		}

		[Test]
		public void 목표가_하나도_없으면_아무데도_못_간다()
		{
			TowerDefenseFlowField field = new(WIDTH, LENGTH, new Vector2Int[0], _ => false);

			Assert.IsFalse(field.IsReachable(new Vector2Int(3, 3)));
		}

	}
}
