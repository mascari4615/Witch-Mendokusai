using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계에 선 것을 화면이 <b>따라 그리려면 무엇을 세우고 무엇을 지워야 하나</b> (TASK-WM-217).
	///
	/// ★ 왜 판정 층인가: 이 판단이 게임 화면 코드 안에 있으면 시험할 수 없다. 그런데 여기를 틀리면
	///   ① 남이 지은 집이 안 보이거나 ② 방금 내가 세운 집이 「세계에 없다」며 도로 지워진다 —
	///   둘 다 사람 눈에는 「짓기가 고장 났다」로 보인다.
	/// </summary>
	public static class BuildingFollowPlan
	{
		/// <summary>
		/// <paramref name="world"/> = 세계가 아는 자리 · <paramref name="shown"/> = 지금 화면에 선 자리 ·
		/// <paramref name="pending"/> = 아직 세계의 답을 기다리는 내 것.
		/// </summary>
		public static void Compute(
			IEnumerable<Vector3Int> world,
			IEnumerable<Vector3Int> shown,
			IEnumerable<Vector3Int> pending,
			List<Vector3Int> toSpawn,
			List<Vector3Int> toDespawn)
		{
			toSpawn.Clear();
			toDespawn.Clear();

			HashSet<Vector3Int> worldCells = new HashSet<Vector3Int>(world);
			HashSet<Vector3Int> shownCells = new HashSet<Vector3Int>(shown);
			HashSet<Vector3Int> pendingCells = pending == null
				? new HashSet<Vector3Int>()
				: new HashSet<Vector3Int>(pending);

			foreach (Vector3Int cell in worldCells)
			{
				if (shownCells.Contains(cell) == false)
					toSpawn.Add(cell);
			}

			foreach (Vector3Int cell in shownCells)
			{
				if (worldCells.Contains(cell))
					continue;

				// 답을 기다리는 내 것은 아직 지우지 않는다 — 방금 세운 집이 깜빡이며 사라진다.
				if (pendingCells.Contains(cell))
					continue;

				toDespawn.Add(cell);
			}
		}

		/// <summary>
		/// 크기를 아는 판(TASK-WM-217). <paramref name="world"/> 는 세계가 아는 <b>건물</b>이다 —
		/// 자리 하나가 아니라 「어디에 · 몇 칸짜리」.
		///
		/// ★ 왜 따로 필요한가: 화면은 여러 칸 건물을 <b>깔고 앉은 칸 전부</b>로 들고 있는데,
		///   세계 쪽을 pivot 한 칸으로만 비교하면 나머지 칸이 매 프레임 「세계에 없는 것」이 된다.
		///   그래서 2×2 를 세우는 순간 3칸이 지워지고, 여러 칸 건물이 한 칸으로 접혔다.
		///
		/// 세울 자리는 언제나 <b>pivot 하나</b>다(칸마다 세우면 한 건물이 네 채가 된다).
		/// 지울 자리는 세계의 어느 건물에도 안 속한 화면 칸이다.
		/// </summary>
		public static void Compute(
			IEnumerable<DomainSDK.Building.BuildingPlacement> world,
			IEnumerable<Vector3Int> shown,
			IEnumerable<Vector3Int> pending,
			List<Vector3Int> toSpawn,
			List<Vector3Int> toDespawn)
		{
			toSpawn.Clear();
			toDespawn.Clear();

			HashSet<Vector3Int> shownCells = new HashSet<Vector3Int>(shown);
			HashSet<Vector3Int> pendingCells = pending == null
				? new HashSet<Vector3Int>()
				: new HashSet<Vector3Int>(pending);

			HashSet<Vector3Int> worldCells = new HashSet<Vector3Int>();
			foreach (DomainSDK.Building.BuildingPlacement building in world)
			{
				Vector3Int pivot = new Vector3Int(building.CellX, building.CellY, building.CellZ);
				Vector2Int size = new Vector2Int(building.WidthOrOne, building.LengthOrOne);

				List<Vector3Int> cells = BuildingFootprint.Cells(pivot, size);
				for (int i = 0; i < cells.Count; i++)
					worldCells.Add(cells[i]);

				// 세우는 건 pivot 에서 한 번 — 화면이 그 건물을 이미 어느 칸으로든 들고 있으면 안 세운다.
				if (shownCells.Contains(pivot) == false)
					toSpawn.Add(pivot);
			}

			foreach (Vector3Int cell in shownCells)
			{
				if (worldCells.Contains(cell))
					continue;

				// 답을 기다리는 내 것은 아직 지우지 않는다 — 방금 세운 집이 깜빡이며 사라진다.
				if (pendingCells.Contains(cell))
					continue;

				toDespawn.Add(cell);
			}
		}
	}
}
