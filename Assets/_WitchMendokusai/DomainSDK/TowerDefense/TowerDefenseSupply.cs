using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 보급선(TASK-WM-194) — 캔 것이 코어까지 *이어져야* 들어온다.
	///
	/// ★ 왜 필요한가: 지금 먼 노드는 「멀다」는 것 외에 위험이 없다. 코어와 이어져 있어야 수입이 되면
	///   넓히는 것이 곧 **지킬 것이 느는 일**이 된다(They Are Billions 의 긴장을 직수입).
	/// ★ 이어짐의 정의 = 내 건물이 징검다리다. 코어에서 시작해 서로 reach 안에 있는 건물끼리 사슬로 잇는다.
	///   중간 건물이 부서지면 그 너머가 통째로 끊긴다 — 「방어선을 길게 늘이면 어딘가는 얇아진다」.
	/// ★ 길찾기와 다른 격자다: 마수는 암반을 피해 걷고, 보급은 *내 건물*을 타고 흐른다.
	///   둘을 같은 것으로 만들면 벽을 세우는 것이 곧 보급이 되어 긴장이 사라진다.
	///
	/// 순수 정적 — 씬·RNG 0.
	/// </summary>
	public static class TowerDefenseSupply
	{
		/// <summary>
		/// 코어에서 사슬로 닿는 건물의 인덱스 집합. buildings[i] 는 건물의 위치(무대 로컬).
		/// reach 안에 다른 *이미 이어진* 건물이 있으면 그 건물도 이어진다(코어가 시작점).
		/// </summary>
		public static void Compute(
			Vector3 corePosition,
			IReadOnlyList<Vector3> buildings,
			float reach,
			HashSet<int> supplied)
		{
			Compute(new[] { corePosition }, buildings, reach, supplied);
		}

		/// <summary>
		/// 시작점이 여럿일 때(코어 + 전초기지) — 전초기지는 *새 보급 원점*이라
		/// 멀리 나간 채집이 코어까지 이어질 필요 없이 가까운 전초기지에 붙으면 된다.
		/// </summary>
		public static void Compute(
			IReadOnlyList<Vector3> seeds,
			IReadOnlyList<Vector3> buildings,
			float reach,
			HashSet<int> supplied)
		{
			supplied.Clear();
			if (buildings == null || buildings.Count == 0 || reach <= 0f || seeds == null)
				return;

			float reachSqr = reach * reach;
			Queue<Vector3> frontier = new();
			foreach (Vector3 seed in seeds)
				frontier.Enqueue(seed);

			// 코어에서 시작해 닿는 것을 계속 넓힌다 — 사슬이 한 칸이라도 끊기면 그 너머는 안 온다.
			while (frontier.Count > 0)
			{
				Vector3 from = frontier.Dequeue();

				for (int index = 0; index < buildings.Count; index++)
				{
					if (supplied.Contains(index))
						continue;
					if ((buildings[index] - from).sqrMagnitude > reachSqr)
						continue;

					supplied.Add(index);
					frontier.Enqueue(buildings[index]);
				}
			}
		}
	}
}
