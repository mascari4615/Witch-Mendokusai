using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary> 연구 성좌 — 갈래·고리·해금 조건 회귀 잠금 (TASK-WM-194). </summary>
	public class TowerDefenseResearchGraphTests
	{
		private static List<TowerDefenseResearchGraph.Node> Build(int branches = 4, int rings = 5)
		{
			List<TowerDefenseResearchGraph.Node> nodes = new();
			TowerDefenseResearchGraph.Build(branches, rings, majorAmount: 0.35f, minorAmount: 0.08f, nodeCost: 2,
				essenceFromRing: 2, resourceNodeCost: 45, nodes);
			return nodes;
		}

		[Test]
		public void 코어는_하나이고_원점에_있다()
		{
			List<TowerDefenseResearchGraph.Node> nodes = Build();

			int coreCount = 0;
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Id == TowerDefenseResearchGraph.CORE_ID)
					coreCount++;
			}

			Assert.AreEqual(1, coreCount);
			Assert.AreEqual(Vector2.zero, nodes[0].Position);
		}

		[Test]
		public void 갈래가_사방으로_퍼진다()
		{
			// 한 줄이 아니라는 것 = 코어에서 나가는 첫 마디가 여럿이라는 것.
			List<TowerDefenseResearchGraph.Node> nodes = Build(branches: 6);

			int fromCore = 0;
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Requires != null && node.Requires.Length == 1
					&& node.Requires[0] == TowerDefenseResearchGraph.CORE_ID)
					fromCore++;
			}

			Assert.AreEqual(6, fromCore, "갈래 수만큼 코어에서 나가야 한다.");
		}

		[Test]
		public void 갈라진_길은_다시_만난다()
		{
			List<TowerDefenseResearchGraph.Node> nodes = Build();

			bool hasMerge = false;
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Requires != null && node.Requires.Length > 1)
				{
					hasMerge = true;
					break;
				}
			}

			Assert.IsTrue(hasMerge, "앞 마디가 둘인 합류 마디가 있어야 고리다.");
		}

		[Test]
		public void 앞_마디를_안_찍으면_못_찍는다()
		{
			List<TowerDefenseResearchGraph.Node> nodes = Build();
			HashSet<int> taken = new();

			TowerDefenseResearchGraph.Node far = nodes[nodes.Count - 1];
			Assert.IsFalse(TowerDefenseResearchGraph.IsReachable(far, taken));

			foreach (int required in far.Requires)
				taken.Add(required);
			Assert.IsTrue(TowerDefenseResearchGraph.IsReachable(far, taken));
		}

		[Test]
		public void 합류_마디는_한_쪽만_찍어도_열린다()
		{
			// 「둘 다 찍어야」면 갈래가 있어도 결국 전부 찍는 한 줄이 된다 — 고르는 뜻이 사라진다.
			List<TowerDefenseResearchGraph.Node> nodes = Build();

			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Requires == null || node.Requires.Length < 2)
					continue;

				HashSet<int> onlyOne = new() { node.Requires[0] };
				Assert.IsTrue(TowerDefenseResearchGraph.IsReachable(node, onlyOne));
				return;
			}

			Assert.Fail("합류 마디가 없다.");
		}

		[Test]
		public void 마디는_겹치지_않는다()
		{
			List<TowerDefenseResearchGraph.Node> nodes = Build();
			HashSet<string> seen = new();

			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				string key = Mathf.RoundToInt(node.Position.x) + ":" + Mathf.RoundToInt(node.Position.y);
				Assert.IsTrue(seen.Add(key), "두 마디가 같은 자리에 있으면 화면에서 하나만 보인다.");
			}
		}
	}
}
