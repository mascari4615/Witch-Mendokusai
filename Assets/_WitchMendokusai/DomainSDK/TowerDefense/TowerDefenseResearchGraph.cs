using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary> 마디가 실제로 무엇을 바꾸나 — 이름만 있는 마디는 「찍을 이유」가 없다. </summary>
	public enum TowerDefenseResearchEffect
	{
		TowerDamage,     // 포탑 피해
		TowerRange,      // 포탑 사거리
		HarvestYield,    // 채집 수입
		SupplyReach,     // 보급이 닿는 거리
		CoreArmor,       // 코어가 버티는 힘
		HeroPower,       // 영웅
	}

	/// <summary>
	/// 연구 성좌(TASK-WM-194) — 코어에서 사방으로 뻗는 *그래프* 형태의 연구도.
	///
	/// ★ 왜 그래프인가 (사용자 지시): 예전 연구는 「단계 1 → 2 → 3」 한 줄이었다. 한 줄짜리 성장은
	///   고를 게 없어서 매 판이 같아진다. 갈래가 뻗고 *다시 만나는* 그래프여야 「이번 판은 어디로
	///   뚫을까」가 판마다 다른 질문이 된다.
	/// ★ 순수 규칙층 — 화면·씬 의존 0. 좌표도 여기서 계산한다(그려주는 쪽이 배치를 또 정하면
	///   두 곳이 갈라진다). 화면은 이 좌표를 그대로 옮겨 그리기만 한다.
	/// </summary>
	public static class TowerDefenseResearchGraph
	{
		/// <summary> 마디 하나. 앞 마디를 전부 찍어야 열린다(and 조건 — or 는 선을 여러 개 그으면 된다). </summary>
		public struct Node
		{
			public int Id;
			public string Name;
			public string Description;
			/// <summary> 큰 마디 = 판을 바꾸는 것(새 건물 해금 등). 작은 마디 = 수치. </summary>
			public bool IsMajor;
			/// <summary> 찍는 데 드는 값. 무엇으로 내는지는 <see cref="UsesEssence"/> 가 정한다. </summary>
			public int Cost;

			/// <summary>
			/// 정수로 사는 마디인가 — 아니면 일반 자원인가.
			///
			/// ★ 안쪽 고리까지 정수로 두면 판 시작에 연구가 통째로 잠긴다 (사용자 실증: "연구 자원이
			///   정수면 초반에 연구 어떻게 하라는 겁니까"). 정수는 *바깥으로 나가야* 나는 것이라
			///   개척을 강요하는 자리는 바깥 고리다. 안쪽 고리는 자원으로 연다 —
			///   연구 단계(<c>TryResearch</c>)가 이미 쓰던 것과 같은 사고방식이다.
			/// </summary>
			public bool UsesEssence;
			/// <summary> 화면 좌표(코어가 원점, 단위 = 화면 픽셀 기준 상대값). </summary>
			public Vector2 Position;
			/// <summary> 이 마디로 들어오는 앞 마디들. 비어 있으면 시작점. </summary>
			public int[] Requires;
			/// <summary> 무엇이 세지나. </summary>
			public TowerDefenseResearchEffect Effect;
			/// <summary> 얼마나 세지나(비율). 큰 마디는 크게. </summary>
			public float Amount;
		}

		public const int CORE_ID = 0;

		/// <summary>
		/// 기본 성좌를 만든다 — 코어에서 <paramref name="branchCount"/> 갈래가 방사로 뻗고,
		/// 갈래마다 중간에서 두 갈래로 갈라졌다가 끝에서 다시 만난다(고리).
		///
		/// ★ 왜 코드가 만드나: 지금은 마디 데이터 자산이 없다. 자산부터 만들면 「빈 화면」을 한참 보게
		///   되므로, *모양이 먼저 서고* 나중에 자산이 이 자리를 대체한다(같은 구조체를 그대로 쓴다).
		/// </summary>
		public static void Build(int branchCount, int ringCount, float majorAmount, float minorAmount,
			int nodeCost, int essenceFromRing, int resourceNodeCost, List<Node> into)
		{
			if (into == null)
				return;
			into.Clear();

			branchCount = Mathf.Max(2, branchCount);
			ringCount = Mathf.Max(2, ringCount);

			into.Add(new Node
			{
				Id = CORE_ID,
				Name = "코어",
				Description = "여기서 모든 길이 뻗어 나간다.",
				IsMajor = true,
				Cost = 0,
				Position = Vector2.zero,
				Requires = System.Array.Empty<int>(),
			});

			const float RING_STEP = 130f;   // 고리 사이 거리 — 선이 겹치지 않는 최소 간격.
			const float FORK_SPREAD = 26f;  // 갈라진 두 길이 벌어지는 각도.

			int nextId = 1;
			for (int branch = 0; branch < branchCount; branch++)
			{
				float baseAngle = 360f / branchCount * branch;
				// 갈래마다 성격이 하나 — 「이 방향은 무엇을 주는가」가 한눈에 읽혀야 고를 이유가 생긴다.
				TowerDefenseResearchEffect theme = (TowerDefenseResearchEffect)(branch % 6);
				int previous = CORE_ID;

				for (int ring = 1; ring <= ringCount; ring++)
				{
					float radius = RING_STEP * ring;
					bool forking = ring > 1 && ring < ringCount;

					if (forking == false)
					{
						// 갈래의 출발점과 도착점 — 도착점이 「큰 마디」다(길 끝에 보상이 있어야 뚫을 이유가 생긴다).
						int id = nextId++;
						into.Add(MakeNode(id, theme, ring, ringCount, baseAngle, radius, previous, majorAmount, minorAmount, nodeCost, essenceFromRing, resourceNodeCost));
						previous = id;
						continue;
					}

					// 가운데 구간은 두 갈래 — 둘 다 찍어도 되고 한쪽만 찍고 지나가도 된다.
					int left = nextId++;
					int right = nextId++;
					into.Add(MakeNode(left, theme, ring, ringCount, baseAngle - FORK_SPREAD, radius, previous, majorAmount, minorAmount, nodeCost, essenceFromRing, resourceNodeCost));
					into.Add(MakeNode(right, theme, ring, ringCount, baseAngle + FORK_SPREAD, radius, previous, majorAmount, minorAmount, nodeCost, essenceFromRing, resourceNodeCost));

					// 다음 고리는 *둘 중 아무거나* 하나면 열린다 — 두 선을 그어 그 뜻을 낸다.
					previous = left;
					int merge = nextId++;
					Node mergeNode = MakeNode(merge, theme, ring + 1, ringCount, baseAngle, RING_STEP * (ring + 1), left, majorAmount, minorAmount, nodeCost, essenceFromRing, resourceNodeCost);
					mergeNode.Requires = new[] { left, right };
					into.Add(mergeNode);
					previous = merge;
					ring++; // 합류 마디가 다음 고리를 이미 차지했다.
				}
			}
		}

		private static Node MakeNode(int id, TowerDefenseResearchEffect theme, int ring, int ringCount,
			float angleDegrees, float radius, int previous, float majorAmount, float minorAmount, int nodeCost,
			int essenceFromRing, int resourceNodeCost)
		{
			float radians = angleDegrees * Mathf.Deg2Rad;
			bool major = ring == ringCount;
			float amount = major ? majorAmount : minorAmount;
			return new Node
			{
				Id = id,
				Effect = theme,
				Amount = amount,
				Name = NameOf(theme) + (major ? " · 끝" : ""),
				Description = NameOf(theme) + " +" + Mathf.RoundToInt(amount * 100f) + "%",
				IsMajor = major,
				// 안쪽 고리는 자원, 바깥 고리부터 정수 — 개척을 강요하는 자리는 바깥이다.
				UsesEssence = ring >= Mathf.Max(1, essenceFromRing),
				Cost = ring >= Mathf.Max(1, essenceFromRing)
					? Mathf.Max(0, nodeCost) * ring
					: Mathf.Max(0, resourceNodeCost) * ring,
				Position = new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius),
				Requires = new[] { previous },
			};
		}

		/// <summary> 효과 이름 — 화면과 규칙이 같은 말을 쓰게 한 곳에 둔다. </summary>
		public static string NameOf(TowerDefenseResearchEffect effect)
		{
			switch (effect)
			{
				case TowerDefenseResearchEffect.TowerDamage: return "포탑 피해";
				case TowerDefenseResearchEffect.TowerRange: return "포탑 사거리";
				case TowerDefenseResearchEffect.HarvestYield: return "채집 수입";
				case TowerDefenseResearchEffect.SupplyReach: return "보급 거리";
				case TowerDefenseResearchEffect.CoreArmor: return "코어 방어";
				default: return "영웅";
			}
		}

		/// <summary> 지금 찍을 수 있나 — 앞 마디 중 하나라도 찍혀 있으면 열린다(선이 곧 조건). </summary>
		public static bool IsReachable(in Node node, ICollection<int> taken)
		{
			if (node.Requires == null || node.Requires.Length == 0)
				return true;
			if (taken == null)
				return false;
			foreach (int required in node.Requires)
			{
				if (taken.Contains(required))
					return true;
			}
			return false;
		}
	}
}
