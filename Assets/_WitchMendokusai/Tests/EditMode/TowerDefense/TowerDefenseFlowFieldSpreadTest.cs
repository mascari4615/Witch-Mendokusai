using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 흐름장이 「여러 최단 경로 중 내 것」을 고르는 규칙 회귀 잠금 (TASK-WM-194).
	///
	/// ★ 왜 잠그나 (사용자 실측): "여전히 거의 한 줄 … 길찾기 알고리즘 좀 씁시다".
	///   원인은 길찾기가 틀린 게 아니라 **같은 거리의 길이 여럿인데 하나만 쓰고 있던 것**이었다.
	///   고친 뒤의 약속은 둘이다 — ① 서로 다른 개체는 서로 다른 칸을 밟을 수 있다
	///   ② 그래도 **걸음 수는 그대로**다(흩어지자고 돌아가면 그건 그냥 길찾기가 나빠진 것).
	///   이 둘이 깨지면 화면에서 다시 한 줄이 되거나 마수가 헤맨다.
	/// </summary>
	public class TowerDefenseFlowFieldSpreadTest
	{
		private const int SIZE = 21;

		private static TowerDefenseFlowField OpenField()
		{
			// 아무것도 막지 않은 벌판 — 최단 경로가 가장 많이 갈리는 판이다.
			return new TowerDefenseFlowField(SIZE, SIZE, new Vector2Int(SIZE / 2, SIZE / 2), _ => false);
		}

		private static List<Vector2Int> Walk(TowerDefenseFlowField field, Vector2Int from, float lane)
		{
			List<Vector2Int> path = new() { from };
			Vector2Int cell = from;
			for (int step = 0; step < SIZE * 4; step++)
			{
				if (field.TryGetNextCell(cell, lane, out Vector2Int next) == false)
					break;
				if (next == cell)
					break;
				cell = next;
				path.Add(cell);
				// 목표에 닿으면 멈춘다 — 목표가 여럿일 때 「도착한 자리」는 *처음 닿은 목표*다.
				// (거리 0 인 칸끼리 계속 걸어가면 도착 자리가 뜻을 잃는다.)
				if (field.DistanceTo(cell) == 0)
					break;
			}
			return path;
		}

		[Test]
		public void 같은_값이면_같은_길을_걷는다()
		{
			// 매번 다른 길을 고르면 마수가 제자리에서 덜덜 떤다.
			TowerDefenseFlowField field = OpenField();
			Vector2Int start = new Vector2Int(0, 0);

			CollectionAssert.AreEqual(Walk(field, start, 0.3f), Walk(field, start, 0.3f));
		}

		[Test]
		public void 값이_다르면_다른_길로_갈라진다()
		{
			// 가로·세로 거리가 다른 자리에서 출발 — 이때 최단 경로가 여럿 생긴다.
			TowerDefenseFlowField field = OpenField();
			Vector2Int start = new Vector2Int(0, 4);

			List<Vector2Int> left = Walk(field, start, 0f);
			List<Vector2Int> right = Walk(field, start, 0.99f);

			CollectionAssert.AreNotEqual(left, right, "같은 길만 나오면 화면에서 다시 한 줄이 된다.");
		}

		[Test]
		public void 정확히_대각선이면_흩어질_수_없다()
		{
			// ★ 이 시험이 잡아낸 한계 — 정직하게 박아 둔다.
			//   대각선 이동이 있는 격자에서 *정확한 대각선* 위에 서면 최단 경로가 **하나뿐**이다.
			//   그래서 「같은 거리 중 내 것 고르기」로는 그 방향의 마수를 못 흩는다.
			//   즉 지금 흩뿌리기는 *비스듬한 방향에서만* 듣는다. 정말로 사방을 넓은 면으로 만들려면
			//   목표를 코어 한 점이 아니라 **코어 둘레의 여러 진입점**으로 나눠야 한다(다음 단계).
			TowerDefenseFlowField field = OpenField();
			Vector2Int diagonal = new Vector2Int(0, 0);

			CollectionAssert.AreEqual(Walk(field, diagonal, 0f), Walk(field, diagonal, 0.99f));
		}

		[Test]
		public void 고리를_목표로_주면_방향마다_다른_자리로_들어온다()
		{
			// ★ 고리가 실제로 주는 것 = *경로의 갈라짐*이 아니라 **도착 자리의 갈라짐**이다.
			//   목표가 코어 한 점이면 사방에서 온 마수가 전부 같은 칸에 겹쳐 선다(한 점 공성).
			//   고리로 나누면 각자 제 방향의 진입점에 붙어 코어를 **둘러싸는** 그림이 된다.
			Vector2Int core = new Vector2Int(SIZE / 2, SIZE / 2);
			List<Vector2Int> goals = new() { core };
			const int RING = 4;
			for (int dx = -RING; dx <= RING; dx++)
			{
				for (int dy = -RING; dy <= RING; dy++)
				{
					if (Mathf.Abs(dx) != RING && Mathf.Abs(dy) != RING)
						continue;
					goals.Add(new Vector2Int(core.x + dx, core.y + dy));
				}
			}

			TowerDefenseFlowField field = new TowerDefenseFlowField(SIZE, SIZE, goals, _ => false);

			List<Vector2Int> west = Walk(field, new Vector2Int(0, SIZE / 2), 0.5f);
			List<Vector2Int> east = Walk(field, new Vector2Int(SIZE - 1, SIZE / 2), 0.5f);
			List<Vector2Int> north = Walk(field, new Vector2Int(SIZE / 2, SIZE - 1), 0.5f);

			Assert.AreNotEqual(west[west.Count - 1], east[east.Count - 1],
				"동·서에서 온 마수가 같은 칸에 서면 둘러싸기가 안 산다.");
			Assert.AreNotEqual(west[west.Count - 1], north[north.Count - 1]);
			Assert.AreNotEqual(east[east.Count - 1], north[north.Count - 1]);
		}

		[Test]
		public void 갈라져도_걸음_수는_그대로다()
		{
			// 흩어지자고 돌아가면 그건 흩어진 게 아니라 길찾기가 나빠진 것이다.
			TowerDefenseFlowField field = OpenField();
			Vector2Int start = new Vector2Int(0, 0);
			int shortest = field.DistanceTo(start);

			for (int i = 0; i <= 10; i++)
			{
				List<Vector2Int> path = Walk(field, start, i / 10f);
				Assert.AreEqual(0, field.DistanceTo(path[path.Count - 1]), "어느 값으로도 목표에 닿아야 한다.");
				Assert.AreEqual(shortest, path.Count - 1, "걸음 수가 최단과 같아야 한다. lane=" + (i / 10f));
			}
		}

		[Test]
		public void 벽이_있으면_모두_돌아간다()
		{
			// 흩뿌리기가 벽을 뚫는 핑계가 되면 안 된다.
			HashSet<Vector2Int> wall = new();
			for (int y = 0; y < SIZE; y++)
			{
				if (y == SIZE / 2)
					continue; // 한 칸만 뚫린 통로
				wall.Add(new Vector2Int(SIZE / 2 - 3, y));
			}

			TowerDefenseFlowField field = new TowerDefenseFlowField(
				SIZE, SIZE, new Vector2Int(SIZE / 2, SIZE / 2), cell => wall.Contains(cell));

			for (int i = 0; i <= 10; i++)
			{
				List<Vector2Int> path = Walk(field, new Vector2Int(0, 0), i / 10f);
				foreach (Vector2Int cell in path)
					Assert.IsFalse(wall.Contains(cell), "막힌 칸을 밟았다. lane=" + (i / 10f));
			}
		}
	}
}
