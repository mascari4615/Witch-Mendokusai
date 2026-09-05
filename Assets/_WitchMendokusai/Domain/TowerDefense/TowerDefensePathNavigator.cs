using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214). 엔진에서 쓰는 건 Transform 같은 씬 손잡이뿐.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

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

		// ITacticNavigator 구현 — 서명은 SDK 좌표 타입이어야 인터페이스를 만족한다 (TASK-WM-214).
		public bool TryGetSteering(Vector3 from, Vector3 to, float lane, out Vector3 direction)
		{
			direction = Vector3.zero;
			if (layout == null || finder == null || stageRoot == null)
				return false;

			Vector3 fromLocal = stageRoot.InverseTransformPoint(from.ToUnity()).ToSim();
			Vector3 goalLocal = stageRoot.InverseTransformPoint(to.ToUnity()).ToSim();

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
			//
			// ★ 「길 자체는 안 바뀐다」는 벽이 없을 때만 참이다. 길찾기는 모서리를 안 뚫는 길을 주는데,
			//   이 섞기가 그 ㄱ자를 대각선으로 되돌려 마수를 바위 모서리에 처박는다 — 마수의 몸 지름은
			//   칸과 같아서(반경 0.50 · 칸 1.00) 벽 모서리 옆 0.13 짜리 틈으로는 못 들어간다.
			//   라이브 실측: 「갈 수 있다는 칸인데 암반에 막힘」이 판마다 36~73줄이었다.
			// ★ 그래서 지름길이 훑는 칸에 벽이 하나라도 있으면 안 섞는다. 트인 곳에서는 그대로 둥글게 돈다
			//   (사용자가 지적한 「뚝뚝 끊기는 움직임」은 트인 곳 이야기다 — 거기 곡선은 그대로 살아 있다).
			if (cornerSmoothing > 0f && follower.Index + 1 < follower.Path.Count
				&& IsShortcutClear(fromCell, follower.Path[follower.Index + 1]))
			{
				nextLocal = Vector3.Lerp(nextLocal, layout.CellToWorld(follower.Path[follower.Index + 1]), cornerSmoothing);
			}

			Vector3 delta = stageRoot.TransformPoint(nextLocal.ToUnity()).ToSim() - from;
			delta.y = 0f;
			if (delta.sqrMagnitude <= 0.0001f)
				return false;

			direction = delta.normalized;
			return true;
		}

		/// <summary>
		/// 지금 칸에서 두 칸 뒤로 질러가도 벽에 안 닿는가 — 사이를 감싸는 네모 안에 벽이 하나라도 있으면 안 된다.
		///
		/// ★ 정밀한 선분 검사가 아니라, 몸이 칸만큼 굵다는 사실에 맞춘 보수적 판정이다.
		///   애매하면 안 질러가는 쪽이 옳다 — 끼면 그 마수는 판이 끝날 때까지 바위를 민다.
		/// </summary>
		private bool IsShortcutClear(Vector2Int fromCell, Vector2Int afterCell)
		{
			int minX = Mathf.Min(fromCell.x, afterCell.x);
			int maxX = Mathf.Max(fromCell.x, afterCell.x);
			int minY = Mathf.Min(fromCell.y, afterCell.y);
			int maxY = Mathf.Max(fromCell.y, afterCell.y);

			for (int x = minX; x <= maxX; x++)
			{
				for (int y = minY; y <= maxY; y++)
				{
					if (layout.IsBlocked(new Vector2Int(x, y)))
						return false;
				}
			}
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
