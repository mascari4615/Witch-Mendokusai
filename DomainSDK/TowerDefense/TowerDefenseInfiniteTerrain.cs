using WitchMendokusai.Numerics;

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

		// ── 광맥 ──────────────────────────────────────────────────────────────────
		// ★ 왜 뭉쳐야 하나 (사용자 지시: "자원이 한곳에 여러 타일이 좀 뭉쳐 있어야 할듯? 광맥처럼"):
		//   자원이 한 점이면 「그 점을 잡았다/못 잡았다」 이분법이고, 채집 건물 하나가 곧 점 하나다.
		//   덩어리로 뭉치면 *얼마나 크게 물고 있나*가 생긴다 — 어디에 세울지, 몇 기를 붙일지가 판단이 된다.
		// ★ 어떻게 뭉치나: 성긴 격자마다 광맥 중심을 하나 두고(있을 수도 없을 수도), 그 중심에서
		//   반경 안에 있는 칸이 광맥 타일이 된다. 반경도 중심마다 달라 덩어리 크기가 제각각이다.

		/// <summary> 광맥 격자 한 칸의 크기(셀) — 광맥끼리의 최소 간격을 정한다. </summary>
		public int VeinSpacing { get; set; } = 26;

		/// <summary> 광맥 격자 칸에 실제로 광맥이 있을 확률. </summary>
		public float VeinChance { get; set; } = 0.55f;

		/// <summary> 광맥 반지름(셀) 범위 — 덩어리 크기가 제각각이어야 판이 심심하지 않다. </summary>
		public float VeinMinRadius { get; set; } = 1.5f;
		public float VeinMaxRadius { get; set; } = 3.5f;

		/// <summary> 그 칸이 광맥 타일인가 — 암반 위엔 안 생기고, 코어 주변 정리 반경도 피한다. </summary>
		public bool IsResourceTile(Vector2Int cell)
		{
			if (ChebyshevDistance(cell, coreCell) <= coreClearRadius)
				return false;
			if (IsBlocked(cell))
				return false;

			int baseX = Mathf.FloorToInt((float)cell.x / VeinSpacing);
			int baseY = Mathf.FloorToInt((float)cell.y / VeinSpacing);

			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					int gridX = baseX + offsetX;
					int gridY = baseY + offsetY;
					if (Hash01(gridX, gridY, 8191) >= VeinChance)
						continue; // 이 격자엔 광맥이 없다.

					Vector2 center = VeinCenter(gridX, gridY);
					float radius = Mathf.Lerp(VeinMinRadius, VeinMaxRadius, Hash01(gridX, gridY, 6151));
					if (Vector2.Distance(new Vector2(cell.x, cell.y), center) <= radius)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// 광맥의 결(TASK-WM-194) — 자원이 뭉치는 것까지는 했는데 광맥끼리 성격이 없었다.
		/// 전부 같으면 「어느 광맥으로 갈까」가 거리 문제로만 남는다. 결이 갈리면 *무엇이 급한가*가 선택이 된다.
		/// </summary>
		public enum VeinKind
		{
			Common = 0, // 흔한 광맥 — 무난하다. 대부분이 이것.
			Rich = 1,   // 굵은 광맥 — 크게 번다. 대신 드물다.
			Deep = 2,   // 깊은 광맥 — 정수가 난다. 안쪽이라도 정수를 준다.
		}

		/// <summary> 그 광맥의 결 — 같은 씨앗이면 언제 물어도 같다. </summary>
		public VeinKind KindOfVeinAt(Vector2Int cell)
		{
			return KindOfVeinGrid(
				Mathf.FloorToInt((float)cell.x / VeinSpacing),
				Mathf.FloorToInt((float)cell.y / VeinSpacing));
		}

		private VeinKind KindOfVeinGrid(int gridX, int gridY)
		{
			float roll = Hash01(gridX, gridY, 9377);
			if (roll < 0.16f)
				return VeinKind.Deep;
			if (roll < 0.38f)
				return VeinKind.Rich;
			return VeinKind.Common;
		}

		/// <summary>
		/// 결에 따른 광맥 *크기* 배수 — 굵은 광맥은 넓어서 채집 하나가 더 많은 칸을 물어 자연히 더 번다.
		/// 벌이 배수를 결로 직접 올리지 않는 이유: 가까운 굵은 광맥이 먼 광맥보다 벌면
		/// 「멀수록 많이 번다」가 깨져 나갈 이유가 사라진다(회귀 FartherNode_PaysMore 가 잡았다).
		/// </summary>
		public static float SizeOf(VeinKind kind)
		{
			return kind switch
			{
				VeinKind.Rich => 1.6f,
				VeinKind.Deep => 0.8f,
				_ => 1f,
			};
		}

		/// <summary> 그 칸이 광맥의 *중심*인가 — 표시(마커)와 노드 목록이 쓰는 대표 좌표. </summary>
		public bool IsVeinCenter(Vector2Int cell)
		{
			int gridX = Mathf.FloorToInt((float)cell.x / VeinSpacing);
			int gridY = Mathf.FloorToInt((float)cell.y / VeinSpacing);
			if (Hash01(gridX, gridY, 8191) >= VeinChance)
				return false;

			Vector2 center = VeinCenter(gridX, gridY);
			return Mathf.RoundToInt(center.x) == cell.x && Mathf.RoundToInt(center.y) == cell.y
				&& IsResourceTile(cell);
		}

		private Vector2 VeinCenter(int gridX, int gridY)
		{
			float jitterX = Hash01(gridX, gridY, 2749);
			float jitterY = Hash01(gridX, gridY, 4211);
			return new Vector2(
				(gridX + jitterX) * VeinSpacing,
				(gridY + jitterY) * VeinSpacing);
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
