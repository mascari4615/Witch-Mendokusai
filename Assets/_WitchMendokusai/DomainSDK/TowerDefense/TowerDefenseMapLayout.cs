using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 자원 노드의 코어 거리 대역. 안전한 안쪽 / 벌이 좋은 바깥쪽. </summary>
	public enum TowerDefenseNodeTier
	{
		Inner = 0, // 코어 방어선 안쪽 — 싸게 먹지만 수입이 작다.
		Outer = 1, // 방어선을 늘려야 닿는 자리 — 수입이 크다. 개척↔방어 긴장의 실체.
	}

	/// <summary> 자원 노드 한 자리. 채집건물은 반드시 이 위(반경 안)에만 설 수 있다. </summary>
	public readonly struct TowerDefenseResourceNodeSpot
	{
		public readonly Vector2Int Cell;
		public readonly Vector3 Position;
		public readonly TowerDefenseNodeTier Tier;

		/// <summary> 이 노드에서 나오는 수입 배수 — 코어에서 멀수록 크다(파라미터의 Near~Far 보간). </summary>
		public readonly float IncomeMultiplier;

		public TowerDefenseResourceNodeSpot(Vector2Int cell, Vector3 position, TowerDefenseNodeTier tier, float incomeMultiplier)
		{
			Cell = cell;
			Position = position;
			Tier = tier;
			IncomeMultiplier = incomeMultiplier;
		}
	}

	/// <summary>
	/// 절차 생성된 특수시공 개척 판 1장 — 코어/적 스폰/자원 노드/암반(통행·배치 불가) 셀.
	/// 순수 데이터(MonoBehaviour·씬 0). 셸(TowerDefenseMatch)이 이걸 읽어 실제 오브젝트를 세운다.
	///
	/// 불변식(생성기가 보장, EditMode 테스트로 고정):
	/// - 모든 적 스폰 지점에서 코어까지 암반을 피해 걸어갈 수 있다.
	/// - 모든 자원 노드에서 코어까지 걸어갈 수 있고, 노드 칸은 암반이 아니다.
	/// - 코어·스폰·노드 주변 정리 반경 안에는 암반이 없다.
	/// </summary>
	public sealed class TowerDefenseMapLayout
	{
		private readonly HashSet<Vector2Int> obstacleCells;
		private readonly List<Vector2Int> obstacleCellList;
		private readonly List<Vector3> enemySpawnPoints;
		private readonly List<TowerDefenseResourceNodeSpot> resourceNodes;

		public int Seed { get; }
		public int Width { get; }
		public int Length { get; }
		public float CellSize { get; }

		public Vector2Int CoreCell { get; }
		public Vector3 CorePosition { get; }

		public IReadOnlyList<Vector3> EnemySpawnPoints => enemySpawnPoints;
		public IReadOnlyList<TowerDefenseResourceNodeSpot> ResourceNodes => resourceNodes;
		public IReadOnlyList<Vector2Int> ObstacleCells => obstacleCellList;

		/// <summary> 판의 월드 가로 크기 — 바닥 평면을 이 크기로 깔면 셀 좌표와 정확히 맞는다. </summary>
		public float GroundWidth => Width * CellSize;
		public float GroundLength => Length * CellSize;

		internal TowerDefenseMapLayout(
			int seed,
			int width,
			int length,
			float cellSize,
			Vector2Int coreCell,
			List<Vector2Int> enemySpawnCells,
			List<TowerDefenseResourceNodeSpot> resourceNodes,
			HashSet<Vector2Int> obstacleCells)
		{
			Seed = seed;
			Width = width;
			Length = length;
			CellSize = cellSize;
			CoreCell = coreCell;

			this.obstacleCells = obstacleCells;
			this.resourceNodes = resourceNodes;

			obstacleCellList = new List<Vector2Int>(obstacleCells);
			obstacleCellList.Sort(CompareCell); // 결정론 — HashSet 열거 순서에 결과가 흔들리지 않게.

			CorePosition = CellToWorld(coreCell);

			enemySpawnPoints = new List<Vector3>(enemySpawnCells.Count);
			for (int i = 0; i < enemySpawnCells.Count; i++)
				enemySpawnPoints.Add(CellToWorld(enemySpawnCells[i]));
		}

		public bool IsInside(Vector2Int cell)
		{
			return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Length;
		}

		/// <summary> 암반 = 유닛 통행·건물 배치 둘 다 불가. 판 밖도 막힌 것으로 본다. </summary>
		public bool IsBlocked(Vector2Int cell)
		{
			if (IsInside(cell) == false)
				return true;
			return obstacleCells.Contains(cell);
		}

		public bool IsBlocked(Vector3 worldPosition)
		{
			return IsBlocked(WorldToCell(worldPosition));
		}

		/// <summary>
		/// 그 자리가 *지금 열려 있는 창* 안인가.
		///
		/// ★ 왜 암반과 갈라야 하나 (무한 맵 1단계): 창 밖은 「막힌 것」으로 취급돼 왔다. 그래서 판 끝에
		///   지으려 하면 화면이 「암반 위엔 못 짓는다」고 *거짓말*을 했다 — 거기엔 암반이 없다.
		///   창이 자라려면 게임이 먼저 「여기가 창 끝이다」를 알아야 한다. 그 앎이 여기서 시작된다.
		/// </summary>
		public bool IsInsideWindow(Vector3 worldPosition)
		{
			return IsInside(WorldToCell(worldPosition));
		}

		/// <summary> 그 자리에서 창 가장자리까지 남은 칸 수 — 창을 언제 넓힐지 정하는 값. </summary>
		public int CellsToWindowEdge(Vector3 worldPosition)
		{
			Vector2Int cell = WorldToCell(worldPosition);
			int toLeft = cell.x;
			int toRight = Width - 1 - cell.x;
			int toBottom = cell.y;
			int toTop = Length - 1 - cell.y;
			return Mathf.Max(0, Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBottom, toTop)));
		}

		/// <summary> 셀 → 셀 중심 월드 좌표(원점 중심 XZ 평면, y=0). </summary>
		public Vector3 CellToWorld(Vector2Int cell)
		{
			float originX = -Width * CellSize * 0.5f;
			float originZ = -Length * CellSize * 0.5f;
			float halfCell = CellSize * 0.5f;
			return new Vector3(
				originX + cell.x * CellSize + halfCell,
				0f,
				originZ + cell.y * CellSize + halfCell);
		}

		/// <summary> 월드 → 셀. 판 밖 좌표도 그대로 환산해 돌려준다(경계 판정은 IsInside 담당). </summary>
		public Vector2Int WorldToCell(Vector3 worldPosition)
		{
			float originX = -Width * CellSize * 0.5f;
			float originZ = -Length * CellSize * 0.5f;
			return new Vector2Int(
				Mathf.FloorToInt((worldPosition.x - originX) / CellSize),
				Mathf.FloorToInt((worldPosition.z - originZ) / CellSize));
		}

		/// <summary> 월드 좌표가 어느 자원 노드의 점유 반경 안인지. 채집건물 배치 판정용. </summary>
		public bool TryFindNodeAt(Vector3 worldPosition, float captureRadius, out int nodeIndex)
		{
			float bestDistanceSqr = captureRadius * captureRadius;
			nodeIndex = -1;

			for (int i = 0; i < resourceNodes.Count; i++)
			{
				Vector3 delta = resourceNodes[i].Position - worldPosition;
				delta.y = 0f;
				float distanceSqr = delta.sqrMagnitude;
				if (distanceSqr > bestDistanceSqr)
					continue;

				bestDistanceSqr = distanceSqr;
				nodeIndex = i;
			}

			return nodeIndex >= 0;
		}

		private static int CompareCell(Vector2Int left, Vector2Int right)
		{
			if (left.y != right.y)
				return left.y.CompareTo(right.y);
			return left.x.CompareTo(right.x);
		}
	}
}
