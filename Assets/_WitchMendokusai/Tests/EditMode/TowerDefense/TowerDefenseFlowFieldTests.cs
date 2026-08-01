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
	}
}
