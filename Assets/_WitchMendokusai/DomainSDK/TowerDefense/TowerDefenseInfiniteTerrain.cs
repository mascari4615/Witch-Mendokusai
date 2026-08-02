using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 경계 없는 지형(TASK-WM-194) — 좌표를 넣으면 그 자리가 무엇인지 *즉석에서* 답한다.
	///
	/// ★ 왜 필요한가 (사용자 지시: "무한 맵으로"): 지금 판은 만들어 두고 쓰는 격자라 반드시 끝이 있다.
	///   끝이 있으면 「넓힌다」에 천장이 생기고, 천장에 닿는 순간 개척이라는 말이 거짓말이 된다.
	///   판을 *저장*하지 않고 *계산*하면 끝이 사라진다 — 어느 좌표를 묻든 답이 나온다.
	///
	/// ★ 왜 해시 기반인가: 같은 씨앗·같은 좌표면 언제 물어도 같은 답이라, 판을 통째로 들고 있지 않아도
	///   화면·길찾기·배치가 서로 다른 지형을 보지 않는다. 저장 0 = 메모리도 좌표 범위와 무관하다.
	///
	/// 지형은 두 층으로 만든다:
	/// ① **구획(Voronoi-lite)** — 좌표를 큰 격자로 나누고 칸마다 흔들린 중심점을 둔다. 가장 가까운 중심과
	///    두 번째로 가까운 중심의 거리 차가 작으면 = 두 구획의 *경계* = 암반 능선.
	///    (경계에만 벽이 서므로 길이 저절로 이어진다 — 무작위로 흩뿌리면 길이 막히는 판이 나온다.)
	/// ② **정리 반경** — 코어 주변은 무조건 비운다. 시작하자마자 벽에 갇히는 판을 원천 차단.
	///
	/// 순수 정적 — 씬·RNG·상태 0. EditMode 로 전량 검증.
	/// </summary>
	public sealed class TowerDefenseInfiniteTerrain
	{
		private readonly int seed;
		private readonly int siteSpacing;
		private readonly float ridgeWidth;
		private readonly float obstacleDensity;
		private readonly Vector2Int coreCell;
		private readonly float coreClearRadius;

		/// <param name="siteSpacing">구획 한 칸의 크기(셀). 클수록 구획이 크고 능선 간격이 넓다.</param>
		/// <param name="ridgeWidth">경계로 인정할 거리 차(셀). 클수록 벽이 두껍다.</param>
		/// <param name="obstacleDensity">능선 후보 중 실제 암반이 되는 비율(0~1). 낮추면 벽이 숭숭 뚫린다.</param>
		public TowerDefenseInfiniteTerrain(
			int seed,
			Vector2Int coreCell,
			int siteSpacing = 12,
			float ridgeWidth = 1.4f,
			float obstacleDensity = 0.7f,
			float coreClearRadius = 6f)
		{
			this.seed = seed;
			this.coreCell = coreCell;
			this.siteSpacing = Mathf.Max(2, siteSpacing);
			this.ridgeWidth = Mathf.Max(0f, ridgeWidth);
			this.obstacleDensity = Mathf.Clamp01(obstacleDensity);
			this.coreClearRadius = Mathf.Max(0f, coreClearRadius);
		}

		/// <summary> 그 칸이 통행·배치 불가(암반)인가. 어느 좌표든 답한다 — 판 밖이라는 것이 없다. </summary>
		public bool IsBlocked(Vector2Int cell)
		{
			// 코어 주변은 언제나 비어 있다 — 시작하자마자 갇히는 판을 만들지 않는다.
			if (ChebyshevDistance(cell, coreCell) <= coreClearRadius)
				return false;

			if (IsOnRidge(cell) == false)
				return false;

			// 능선 위라도 일부만 실제 암반 — 벽이 통짜면 길이 한 줄로 굳는다.
			return Hash01(cell.x, cell.y, 7717) < obstacleDensity;
		}

		/// <summary> 두 구획의 경계(능선) 위인가 — 벽이 설 수 있는 자리. </summary>
		public bool IsOnRidge(Vector2Int cell)
		{
			float nearest = float.MaxValue;
			float second = float.MaxValue;

			int baseX = Mathf.FloorToInt((float)cell.x / siteSpacing);
			int baseY = Mathf.FloorToInt((float)cell.y / siteSpacing);

			// 이웃 구획 3×3 만 보면 가장 가까운 둘을 놓치지 않는다(중심이 자기 칸 안에서만 흔들리므로).
			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					Vector2 site = SiteOf(baseX + offsetX, baseY + offsetY);
					float distance = Vector2.Distance(new Vector2(cell.x, cell.y), site);
					if (distance < nearest)
					{
						second = nearest;
						nearest = distance;
					}
					else if (distance < second)
					{
						second = distance;
					}
				}
			}

			return second - nearest <= ridgeWidth;
		}

		/// <summary> 구획 (gridX, gridY) 의 중심점 — 칸 안에서 결정적으로 흔들린다(격자 티가 안 나게). </summary>
		private Vector2 SiteOf(int gridX, int gridY)
		{
			float jitterX = Hash01(gridX, gridY, 1013);
			float jitterY = Hash01(gridX, gridY, 3571);
			return new Vector2(
				(gridX + jitterX) * siteSpacing,
				(gridY + jitterY) * siteSpacing);
		}

		/// <summary> 그 칸이 자원 노드인가 — 능선이 아닌 자리에만, 드문드문. </summary>
		public bool IsResourceNode(Vector2Int cell, float nodeChance = 0.012f)
		{
			if (ChebyshevDistance(cell, coreCell) <= coreClearRadius)
				return false;
			if (IsBlocked(cell))
				return false;

			return Hash01(cell.x, cell.y, 5273) < Mathf.Clamp01(nodeChance);
		}

		/// <summary>
		/// 코어에서 그 칸까지의 거리로 정해지는 벌이 배수 — 멀수록 크다.
		/// 무한 판이라 상한이 필요하다: 무한히 멀리 가면 무한히 번다는 규칙은 게임이 아니다.
		/// </summary>
		public float IncomeMultiplierAt(Vector2Int cell, float nearMultiplier, float farMultiplier, float fullDistance)
		{
			if (fullDistance <= 0f)
				return nearMultiplier;

			float ratio = Mathf.Clamp01(ChebyshevDistance(cell, coreCell) / fullDistance);
			return Mathf.Lerp(nearMultiplier, farMultiplier, ratio);
		}

		private static float ChebyshevDistance(Vector2Int from, Vector2Int to)
		{
			return Mathf.Max(Mathf.Abs(from.x - to.x), Mathf.Abs(from.y - to.y));
		}

		// 좌표 해시 → 0~1. 같은 입력이면 언제나 같은 값(그래야 판을 안 들고 있어도 일관된다).
		private float Hash01(int x, int y, int salt)
		{
			unchecked
			{
				int hash = seed;
				hash = hash * 486187739 + x;
				hash = hash * 486187739 + y;
				hash = hash * 486187739 + salt;
				hash ^= hash >> 15;
				hash *= 668265261;
				hash ^= hash >> 13;
				return (hash & 0x7fffffff) / (float)0x7fffffff;
			}
		}
	}
}
