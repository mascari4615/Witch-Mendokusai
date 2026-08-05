using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
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
			/// <summary> 찍는 데 드는 정수. </summary>
			public int Cost;
			/// <summary> 화면 좌표(코어가 원점, 단위 = 화면 픽셀 기준 상대값). </summary>
			public Vector2 Position;
			/// <summary> 이 마디로 들어오는 앞 마디들. 비어 있으면 시작점. </summary>
			public int[] Requires;
		}

		public const int CORE_ID = 0;

		/// <summary>
		/// 기본 성좌를 만든다 — 코어에서 <paramref name="branchCount"/> 갈래가 방사로 뻗고,
		/// 갈래마다 중간에서 두 갈래로 갈라졌다가 끝에서 다시 만난다(고리).
		///
		/// ★ 왜 코드가 만드나: 지금은 마디 데이터 자산이 없다. 자산부터 만들면 「빈 화면」을 한참 보게
		///   되므로, *모양이 먼저 서고* 나중에 자산이 이 자리를 대체한다(같은 구조체를 그대로 쓴다).
		/// </summary>
		public static void Build(int branchCount, int ringCount, List<Node> into)
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
				int previous = CORE_ID;

				for (int ring = 1; ring <= ringCount; ring++)
				{
					float radius = RING_STEP * ring;
					bool forking = ring > 1 && ring < ringCount;

					if (forking == false)
					{
						// 갈래의 출발점과 도착점 — 도착점이 「큰 마디」다(길 끝에 보상이 있어야 뚫을 이유가 생긴다).
						int id = nextId++;
						into.Add(MakeNode(id, branch, ring, ringCount, baseAngle, radius, previous));
						previous = id;
						continue;
					}

					// 가운데 구간은 두 갈래 — 둘 다 찍어도 되고 한쪽만 찍고 지나가도 된다.
					int left = nextId++;
					int right = nextId++;
					into.Add(MakeNode(left, branch, ring, ringCount, baseAngle - FORK_SPREAD, radius, previous));
					into.Add(MakeNode(right, branch, ring, ringCount, baseAngle + FORK_SPREAD, radius, previous));

					// 다음 고리는 *둘 중 아무거나* 하나면 열린다 — 두 선을 그어 그 뜻을 낸다.
					previous = left;
					int merge = nextId++;
					Node mergeNode = MakeNode(merge, branch, ring + 1, ringCount, baseAngle, RING_STEP * (ring + 1), left);
					mergeNode.Requires = new[] { left, right };
					into.Add(mergeNode);
					previous = merge;
					ring++; // 합류 마디가 다음 고리를 이미 차지했다.
				}
			}
		}

		private static Node MakeNode(int id, int branch, int ring, int ringCount,
			float angleDegrees, float radius, int previous)
		{
			float radians = angleDegrees * Mathf.Deg2Rad;
			bool major = ring == ringCount;
			return new Node
			{
				Id = id,
				Name = major ? "길 끝" : "마디",
				Description = major ? "이 갈래를 끝까지 뚫으면 얻는 것." : "조금씩 세진다.",
				IsMajor = major,
				Cost = ring,
				Position = new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius),
				Requires = new[] { previous },
			};
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
