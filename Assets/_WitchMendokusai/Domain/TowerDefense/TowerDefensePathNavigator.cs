using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 격자 A* 로 *실제 경로*를 찾아 따라가게 하는 안내자 (TASK-WM-194).
	///
	/// ★ 왜 흐름장을 갈아치웠나 (사용자 지시: "길찾기 알고리즘을 쓰라는거야 … 정석 근본 구현"):
	///   흐름장은 「모두가 코어로」에만 답한다. 목표가 코어가 아니면(눈앞의 벽·포탑) 안내를 포기했고,
	///   그때마다 마수가 **직선으로 걷다 벽에 박혔다**. 박힌 것을 「한 칸 밀어주기」로 떠밀어
	///   길을 뚫는 임시방편까지 붙어 있었다(사용자 실증: "밀어주는게 어딨어").
	///   목표가 무엇이든 답하는 길찾기면 그 임시방편이 통째로 필요 없다.
	///
	/// ★ 개체마다 자기 경로를 들고 다닌다. 매 프레임 새로 찾지 않는다 — 목표 칸이 바뀌었거나,
	///   판이 바뀌었거나(벽이 서고 부서짐), 길에서 벗어났을 때만 다시 찾는다.
	///   그래서 마수 수십이 걸어도 탐색은 드문드문 일어난다.
	///
	/// ★ 길이 없으면 **안내하지 않는다**(false). 직선으로 되돌리지 않는다 — 그게 벽 뚫기의 근원이었다.
	///   호출자는 그 사실로 「막혔으니 앞의 벽을 부순다」를 판단한다.
	/// </summary>
	public sealed class TowerDefensePathNavigator : ITacticNavigator
	{
		private readonly TowerDefenseMapLayout layout;
		private readonly TowerDefenseGridPath finder;
		private readonly Transform stageRoot;
		private readonly float directGoalDistance;
		private readonly float cornerSmoothing;

		/// <summary> 판이 바뀐 횟수 — 벽이 서거나 부서지면 올린다. 들고 있던 경로는 그 순간 낡은 것이 된다. </summary>
		public int Version { get; private set; }

		public void Invalidate() => Version++;

		public TowerDefensePathNavigator(
			TowerDefenseMapLayout layout,
			TowerDefenseGridPath finder,
			Transform stageRoot,
			float directGoalDistance,
			float cornerSmoothing = 0f)
		{
			this.layout = layout;
			this.finder = finder;
			this.stageRoot = stageRoot;
			this.directGoalDistance = directGoalDistance;
			this.cornerSmoothing = Mathf.Clamp01(cornerSmoothing);
		}

		private sealed class Follower
		{
			public readonly List<Vector2Int> Path = new();
			public int Index;
			public Vector2Int Goal;
			public int Version = -1;
		}

		// 개체별 경로 — 키는 그 개체의 lane 이 아니라 *부른 쪽이 준 표식*이다(아래 참고).
		private readonly Dictionary<int, Follower> followers = new();

		/// <summary>
		/// ITacticNavigator 계약대로 방향만 돌려준다.
		/// ★ lane 을 개체 표식으로도 쓴다 — 인터페이스에 개체 id 가 없고, lane 은 개체마다 다른 고정값이라
		///   그대로 열쇠가 된다(같은 값이 겹치면 경로를 나눠 쓰는 것뿐 — 길이는 같으니 해가 없다).
		/// </summary>
		/// <summary> 길을 못 찾아 안내를 포기한 횟수 — 「부수러 가는 중」과 「그냥 서 있음」을 가르는 첫 숫자. </summary>
		public int NoPathCount { get; private set; }

		public bool TryGetSteering(Vector3 from, Vector3 to, float lane, out Vector3 direction)
		{
			direction = Vector3.zero;
			if (layout == null || finder == null || stageRoot == null)
				return false;

			Vector3 fromLocal = stageRoot.InverseTransformPoint(from);
			Vector3 goalLocal = stageRoot.InverseTransformPoint(to);

			// 목표가 코앞이면 칸 단위 안내보다 직선이 정확하다 — 마지막 한 칸에서 덜덜 떠는 것 방지.
			if ((goalLocal - fromLocal).sqrMagnitude <= directGoalDistance * directGoalDistance)
				return false;

			Vector2Int fromCell = layout.WorldToCell(fromLocal);
			Vector2Int goalCell = layout.WorldToCell(goalLocal);
			if (fromCell == goalCell)
				return false;

			int key = Mathf.RoundToInt(lane * 100000f);
			if (followers.TryGetValue(key, out Follower follower) == false)
			{
				follower = new Follower();
				followers[key] = follower;
			}

			bool needsSearch = follower.Path.Count == 0
				|| follower.Goal != goalCell
				|| follower.Version != Version
				|| follower.Index >= follower.Path.Count
				|| OffPath(follower, fromCell);

			if (needsSearch)
			{
				if (finder.Find(fromCell, goalCell, lane, follower.Path) == false)
				{
					follower.Path.Clear();
					// ★ 「길이 없다」는 *정상일 수도, 교착일 수도* 있다 — 앞을 막은 벽을 부수러 붙는 중이면
					//   정상이고, 아무도 안 부수고 서 있으면 판이 안 끝난다. 세어 두지 않으면 그 둘을
					//   영영 못 가른다(처음 A* 를 넣을 때 「완전히 둘러싸였을 때는 보장 안 됨」으로 남긴 자리다).
					NoPathCount++;
					return false; // 직선으로 넘기지 않는다(그게 벽 뚫기의 근원이었다).
				}

				follower.Goal = goalCell;
				follower.Version = Version;
				follower.Index = 0;
			}

			// 이미 지나온 칸은 버린다 — 몸이 앞서 있는데 뒤 칸을 가리키면 제자리에서 흔들린다.
			while (follower.Index < follower.Path.Count && follower.Path[follower.Index] == fromCell)
				follower.Index++;
			if (follower.Index >= follower.Path.Count)
				return false;

			Vector3 nextLocal = layout.CellToWorld(follower.Path[follower.Index]);

			// 한 칸 더 내다보고 섞는다 — 같은 길을 곡선으로 걷는다(길 자체는 안 바뀐다).
			if (cornerSmoothing > 0f && follower.Index + 1 < follower.Path.Count)
				nextLocal = Vector3.Lerp(nextLocal, layout.CellToWorld(follower.Path[follower.Index + 1]), cornerSmoothing);

			Vector3 delta = stageRoot.TransformPoint(nextLocal) - from;
			delta.y = 0f;
			if (delta.sqrMagnitude <= 0.0001f)
				return false;

			direction = delta.normalized;
			return true;
		}

		/// <summary> 길에서 벗어났나 — 지금 칸이 앞으로 밟을 몇 칸 안에 없으면 새로 찾는다. </summary>
		private static bool OffPath(Follower follower, Vector2Int cell)
		{
			int last = Mathf.Min(follower.Path.Count - 1, follower.Index + 2);
			for (int index = follower.Index; index <= last; index++)
			{
				Vector2Int step = follower.Path[index];
				if (Mathf.Abs(step.x - cell.x) <= 1 && Mathf.Abs(step.y - cell.y) <= 1)
					return false;
			}

			return true;
		}

		/// <summary> 판이 끝나면 들고 있던 경로도 버린다 — 다음 판이 지난 판의 길을 물고 시작하지 않게. </summary>
		public void Clear()
		{
			followers.Clear();
			Version++;
		}
	}
}
