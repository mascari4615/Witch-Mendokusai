using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 흩뿌린 서식지 — 판 곳곳에 마수가 **미리 깔려 있다** (TASK-WM-194, 데아빌 레퍼런스).
	///
	/// ★ 왜 필요한가 (사용자 선택): 파도만 있으면 판 안쪽은 파도 사이에 완전히 안전하다. 그러면
	///   「넓힌다」의 대가가 *나중에 지킬 면적이 는다*뿐이라 미뤄도 손해가 없다. 서식지가 깔려 있으면
	///   **넓히는 행위 자체가 위험**이 된다 — 그게 「개척」이라는 말이 성립하는 자리다.
	/// ★ 파도와 다른 층이다: 파도는 *시간*이 정하고(예고됨), 서식지는 *내가 어디로 가느냐*가 정한다.
	///   둘을 합치면 「언제 올지」와 「어디를 건드릴지」 중 하나가 사라진다.
	///
	/// ★ 왜 결정론인가: 같은 씨앗이면 같은 판이어야 다시 도전하는 의미가 생기고,
	///   저장·복원 때 서식지가 딴 데로 옮겨가지 않는다.
	///
	/// 순수 정적 — 씬·전역 RNG 0. EditMode 로 전량 검증.
	/// </summary>
	public static class TowerDefenseLairPlacement
	{
		/// <summary>
		/// 서식지 자리를 고른다. 규칙 셋: ① 코어에서 <paramref name="minCoreDistance"/> 밖 ②
		/// 서식지끼리 <paramref name="minSpacing"/> 밖 ③ 지나갈 수 있는 칸(막힌 칸엔 못 선다).
		///
		/// 자리가 모자라면 찾은 만큼만 돌려준다 — 억지로 채우면 규칙 ①②를 깨서
		/// 「코어 옆에 서식지가 붙어 시작하자마자 죽는」 판이 나온다.
		/// </summary>
		public static void Choose(
			int seed,
			int width,
			int length,
			Vector2Int coreCell,
			System.Func<Vector2Int, bool> isBlocked,
			int count,
			float minCoreDistance,
			float minSpacing,
			List<Vector2Int> into)
		{
			if (into == null)
				return;

			into.Clear();
			if (count <= 0 || width <= 0 || length <= 0)
				return;

			// ① 규칙 ①③ 을 통과하는 후보를 전부 모은다.
			List<Vector2Int> candidates = new();
			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Vector2Int cell = new(x, y);
					if (isBlocked != null && isBlocked(cell))
						continue;
					if (Vector2Int.Distance(cell, coreCell) < minCoreDistance)
						continue;
					candidates.Add(cell);
				}
			}

			if (candidates.Count == 0)
				return;

			// ② **가장 먼 자리부터** 하나씩 고른다(farthest-point sampling).
			//
			// ★ 격자를 정해진 걸음으로 건너뛰며 고르는 방식은 판 한쪽에 쏠린다 — 시험이 그걸 잡았다
			//   ("네 방향 모두에 서식지가 있어야 판 전체가 위험하다"). 쏠리면 반대편으로 넓히는 데
			//   아무 위험이 없어서 「어느 쪽으로 넓힐까」라는 질문 자체가 죽는다.
			//   이미 고른 것들에서 *가장 멀리 떨어진* 후보를 계속 고르면 구조적으로 퍼진다.
			int first = ((seed % candidates.Count) + candidates.Count) % candidates.Count; // 씨앗이 첫 자리를 정한다.
			into.Add(candidates[first]);

			while (into.Count < count)
			{
				int bestIndex = -1;
				float bestDistance = -1f;

				for (int index = 0; index < candidates.Count; index++)
				{
					float nearest = NearestDistance(into, candidates[index]);
					if (nearest < minSpacing)
						continue;
					if (nearest <= bestDistance)
						continue; // 동률이면 낮은 번호가 이긴다 = 결정론.

					bestDistance = nearest;
					bestIndex = index;
				}

				if (bestIndex < 0)
					return; // 규칙을 지키면서 더 놓을 자리가 없다 — 억지로 채우지 않는다.

				into.Add(candidates[bestIndex]);
			}
		}

		private static float NearestDistance(List<Vector2Int> chosen, Vector2Int cell)
		{
			float nearest = float.MaxValue;
			for (int index = 0; index < chosen.Count; index++)
			{
				float distance = Vector2Int.Distance(chosen[index], cell);
				if (distance < nearest)
					nearest = distance;
			}
			return nearest;
		}

		/// <summary>
		/// 그 서식지가 깨어나는가 — 내 것 중 하나라도 <paramref name="wakeRadius"/> 안에 들어왔으면 깬다.
		///
		/// ★ 「가까이 가면 깬다」여야 넓히는 것이 위험이 된다. 처음부터 다 깨어 있으면 그냥 파도가
		///   하나 더 있는 것이고, 영영 안 깨면 판을 장식하는 조형물이다.
		/// </summary>
		public static bool ShouldWake(Vector3 lairPosition, IReadOnlyList<Vector3> myBuildings, float wakeRadius)
		{
			if (myBuildings == null || wakeRadius <= 0f)
				return false;

			float squared = wakeRadius * wakeRadius;
			for (int index = 0; index < myBuildings.Count; index++)
			{
				if ((myBuildings[index] - lairPosition).sqrMagnitude <= squared)
					return true;
			}
			return false;
		}

	}
}
