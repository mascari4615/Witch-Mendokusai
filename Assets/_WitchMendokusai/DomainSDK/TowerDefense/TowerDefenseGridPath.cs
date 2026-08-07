using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 격자 A* — *아무 칸에서 아무 칸으로* 실제 경로를 찾는다.
	///
	/// ★ 왜 흐름장 대신 이것인가: 흐름장은 「모두가 한 곳(코어)으로」에만 답한다. 그런데 마수는
	///   코어만 노리지 않는다 — 앞을 막은 벽·포탑도 노린다. 목표가 코어가 아닌 순간 흐름장은
	///   손을 떼고, 그때마다 마수가 **직선으로 걷다 벽에 박혔다**(그걸 가리려고 「한 칸 밀어주기」라는
	///   임시방편까지 붙어 있었다). 목표가 무엇이든 답하는 길찾기가 있어야 그 전부가 사라진다.
	///
	/// ★ 결정적이다 — 같은 판·같은 목표면 항상 같은 길. 무작위를 안 쓰고, 동점은 정해진 순서로 깬다.
	///   대신 개체마다 *다른 길*을 원하면 <see cref="Find"/> 에 lane 을 준다: 같은 길이의 후보 중
	///   어느 쪽을 먼저 볼지가 갈려, 무리가 한 줄로 겹치지 않으면서도 재현성은 유지된다.
	///
	/// ★ 8방향 이동이지만 **모서리를 뚫지 않는다** — 대각선은 양옆 두 칸이 모두 열려 있을 때만.
	///   안 그러면 벽 사이 틈으로 몸이 통과하는 것처럼 보인다.
	/// </summary>
	public sealed class TowerDefenseGridPath
	{
		private readonly int width;
		private readonly int length;
		private readonly System.Func<Vector2Int, bool> isBlocked;

		// 열린 목록 = 이진 힙(작은 값 먼저). List 로 매번 최소를 훑으면 판이 커질수록 급격히 느려진다.
		private readonly List<Vector2Int> heap = new();
		private readonly Dictionary<Vector2Int, float> gScore = new();
		private readonly Dictionary<Vector2Int, float> fScore = new();
		private readonly Dictionary<Vector2Int, Vector2Int> cameFrom = new();
		private readonly HashSet<Vector2Int> closed = new();

		/// <summary>
		/// 한 번의 탐색이 열어볼 수 있는 칸 수 상한 — 길이 없을 때 판 전체를 훑고 프레임을 잡아먹지 않게.
		///
		/// ★ **판 크기에서 나온다.** 예전엔 4000 이라는 상수였는데, 판은 200×200(=40000칸)까지 자란다 —
		///   상수는 판이 커질수록 *조용히* 모자라진다. 실측에서 이미 한 번에 2196칸(상한의 55%)을
		///   펼치고 있었다. 넘는 순간의 증상은 「몇 마리가 그냥 안 움직인다」뿐이라 원인을 짚기가 어렵다.
		///   판의 절반이면 막다른 길을 훑기엔 넉넉하고, 폭주는 여전히 막는다.
		/// </summary>
		public int MaxExpandedCells { get; set; }

		/// <summary> 마지막 탐색이 연 칸 수 — 비용을 눈으로 볼 수 있어야 느려질 때 원인을 짚는다. </summary>
		public int LastExpandedCells { get; private set; }

		/// <summary>
		/// 상한에 걸려 「길 없음」으로 끝난 횟수 — **0 이 아니면 마수가 갈 길이 있는데도 못 간다.**
		/// 상한은 판이 커지면 모자랄 수 있는데, 그 증상은 「몇 마리가 그냥 안 움직인다」로만 보여서
		/// 원인을 길찾기로 짚기가 어렵다. 그래서 규칙층이 직접 센다.
		/// </summary>
		public int CapHits { get; private set; }

		/// <summary> 지금까지 한 번의 탐색에서 가장 많이 펼친 칸 수 — 상한에 얼마나 가까운지 본다. </summary>
		public int PeakExpandedCells { get; private set; }

		public TowerDefenseGridPath(int width, int length, System.Func<Vector2Int, bool> isBlocked)
		{
			this.width = width;
			this.length = length;
			this.isBlocked = isBlocked;
			// 상한은 판에서 파생된다 — 상수로 두면 판이 커질 때 아무도 안 고치고 조용히 모자라진다.
			MaxExpandedCells = Mathf.Max(4000, width * length / 2);
		}

		private static readonly Vector2Int[] STEPS =
		{
			new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
			new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
		};

		private bool InBounds(Vector2Int cell) =>
			cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < length;

		private bool Walkable(Vector2Int cell) => InBounds(cell) && isBlocked(cell) == false;

		/// <summary>
		/// from 에서 goal 까지의 길을 into 에 채운다(from 제외, goal 포함). 못 찾으면 false 이고 into 는 빈다.
		///
		/// ★ goal 이 막힌 칸이어도 찾는다 — 마수의 목표는 *부술 벽*일 때가 많고, 벽 칸은 당연히 막혀 있다.
		///   그 경우 「벽에 닿는 칸」까지 안내한다(닿으면 때린다). 이게 없으면 「목표는 있는데 갈 수 없다」가 된다.
		/// </summary>
		public bool Find(Vector2Int from, Vector2Int goal, float lane, List<Vector2Int> into)
		{
			into.Clear();
			LastExpandedCells = 0;
			if (InBounds(from) == false || InBounds(goal) == false)
				return false;

			bool goalIsBlocked = isBlocked(goal);
			if (from == goal)
				return false;

			heap.Clear();
			gScore.Clear();
			fScore.Clear();
			cameFrom.Clear();
			closed.Clear();

			gScore[from] = 0f;
			fScore[from] = Heuristic(from, goal);
			HeapPush(from);

			// 같은 값이면 어느 이웃을 먼저 볼지 — 개체마다 다른 줄을 밟게 하는 유일한 흔들림.
			int laneOffset = Mathf.Clamp(Mathf.FloorToInt(lane * STEPS.Length), 0, STEPS.Length - 1);

			while (heap.Count > 0)
			{
				Vector2Int current = HeapPop();
				if (closed.Add(current) == false)
					continue;

				if (current == goal)
				{
					Rebuild(current, into);
					return true;
				}

				LastExpandedCells++;
				if (LastExpandedCells > PeakExpandedCells)
					PeakExpandedCells = LastExpandedCells;
				if (LastExpandedCells > MaxExpandedCells)
				{
					CapHits++; // 「길이 없다」가 아니라 「그만 찾았다」다 — 둘을 뭉치면 원인을 영영 못 찾는다.
					break;
				}

				for (int index = 0; index < STEPS.Length; index++)
				{
					Vector2Int step = STEPS[(index + laneOffset) % STEPS.Length];
					Vector2Int next = current + step;
					if (InBounds(next) == false)
						continue;

					// 목표 칸은 막혀 있어도 들어간다(부술 대상) — 그 외 막힌 칸은 안 밟는다.
					bool isGoal = next == goal;
					if (isGoal == false && isBlocked(next))
						continue;

					// 대각선은 모서리를 뚫지 않는다 — 양옆이 둘 다 열려야 지나간다.
					if (step.x != 0 && step.y != 0)
					{
						if (Walkable(new Vector2Int(current.x + step.x, current.y)) == false
							|| Walkable(new Vector2Int(current.x, current.y + step.y)) == false)
							continue;
					}

					float stepCost = (step.x != 0 && step.y != 0) ? 1.41421356f : 1f;
					float tentative = gScore[current] + stepCost;
					if (gScore.TryGetValue(next, out float known) && tentative >= known)
						continue;

					cameFrom[next] = current;
					gScore[next] = tentative;
					fScore[next] = tentative + Heuristic(next, goal);
					HeapPush(next);
				}
			}

			// 막힌 목표를 못 뚫었을 때 = 「닿을 수 없다」. 부를 쪽이 그 사실로 판단한다(직선으로 넘기지 않는다).
			_ = goalIsBlocked;
			return false;
		}

		/// <summary> 대각선을 아는 휴리스틱(옥타일) — 실제 이동 비용과 같은 자를 써야 길이 휘지 않는다. </summary>
		private static float Heuristic(Vector2Int a, Vector2Int b)
		{
			int dx = Mathf.Abs(a.x - b.x);
			int dy = Mathf.Abs(a.y - b.y);
			int min = Mathf.Min(dx, dy);
			return (dx + dy) + (1.41421356f - 2f) * min;
		}

		private void Rebuild(Vector2Int goal, List<Vector2Int> into)
		{
			Vector2Int cursor = goal;
			while (cameFrom.TryGetValue(cursor, out Vector2Int previous))
			{
				into.Add(cursor);
				cursor = previous;
			}

			into.Reverse();
		}

		private void HeapPush(Vector2Int cell)
		{
			heap.Add(cell);
			int child = heap.Count - 1;
			while (child > 0)
			{
				int parent = (child - 1) / 2;
				if (Score(heap[parent]) <= Score(heap[child]))
					break;
				(heap[parent], heap[child]) = (heap[child], heap[parent]);
				child = parent;
			}
		}

		private Vector2Int HeapPop()
		{
			Vector2Int top = heap[0];
			heap[0] = heap[heap.Count - 1];
			heap.RemoveAt(heap.Count - 1);

			int parent = 0;
			while (true)
			{
				int left = parent * 2 + 1;
				int right = left + 1;
				int smallest = parent;
				if (left < heap.Count && Score(heap[left]) < Score(heap[smallest]))
					smallest = left;
				if (right < heap.Count && Score(heap[right]) < Score(heap[smallest]))
					smallest = right;
				if (smallest == parent)
					break;
				(heap[parent], heap[smallest]) = (heap[smallest], heap[parent]);
				parent = smallest;
			}

			return top;
		}

		private float Score(Vector2Int cell) => fScore.TryGetValue(cell, out float score) ? score : float.MaxValue;
	}
}
