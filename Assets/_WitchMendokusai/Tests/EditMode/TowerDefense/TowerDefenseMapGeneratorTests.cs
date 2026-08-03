using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 판 생성기 회귀 — *매 판이 어떤 판인가*를 통째로 정하는 규칙인데 시험이 하나도 없었다 (TASK-WM-194).
	///
	/// ★ 왜 이제 붙이나: 이 근처에서 실제 사고가 났다 — 광맥 밀도를 암반 수에서 뽑고 있어서
	///   암반이 없는 판에서 **자원이 통째로 사라졌다**. 시험이 있었으면 그때 잡혔다.
	/// ★ 씨앗만 고정하면 전부 순수 계산이라 씬·물리 0 으로 확인된다.
	/// </summary>
	public class TowerDefenseMapGeneratorTests
	{
		private static TowerDefenseMapParameters Parameters(int seed)
		{
			TowerDefenseMapParameters parameters = TowerDefenseMapParameters.Default;
			parameters.Seed = seed;
			return parameters;
		}

		[Test]
		public void 같은_씨앗이면_같은_판이_나온다()
		{
			// ★ 씨앗 공유가 이것 위에 서 있다 — 깨지면 「이 씨앗 해봐」가 거짓말이 된다.
			TowerDefenseMapLayout first = TowerDefenseMapGenerator.Generate(Parameters(20260804));
			TowerDefenseMapLayout second = TowerDefenseMapGenerator.Generate(Parameters(20260804));

			Assert.AreEqual(first.CoreCell, second.CoreCell);
			Assert.AreEqual(first.ObstacleCells.Count, second.ObstacleCells.Count, "암반이 다르면 다른 판이다.");
			Assert.AreEqual(first.ResourceNodes.Count, second.ResourceNodes.Count);
			Assert.AreEqual(first.EnemySpawnPoints.Count, second.EnemySpawnPoints.Count);

			for (int index = 0; index < first.ResourceNodes.Count; index++)
				Assert.AreEqual(first.ResourceNodes[index].Cell, second.ResourceNodes[index].Cell, $"노드 {index} 자리가 다르다.");
		}

		[Test]
		public void 다른_씨앗이면_다른_판이_나온다()
		{
			// 안 그러면 매 판 생성이 장식이 된다.
			TowerDefenseMapLayout first = TowerDefenseMapGenerator.Generate(Parameters(1));
			TowerDefenseMapLayout second = TowerDefenseMapGenerator.Generate(Parameters(2));

			bool sameObstacleCount = first.ObstacleCells.Count == second.ObstacleCells.Count;
			bool sameFirstNode = first.ResourceNodes.Count > 0 && second.ResourceNodes.Count > 0
				&& first.ResourceNodes[0].Cell == second.ResourceNodes[0].Cell;

			Assert.IsFalse(sameObstacleCount && sameFirstNode, "씨앗이 달라도 판이 같으면 씨앗이 아무 일도 안 하는 것이다.");
		}

		[Test]
		public void 암반을_아예_안_깔아도_자원은_나온다()
		{
			// ★ 실제로 났던 사고 — 광맥 밀도를 암반 수에서 뽑아서, 암반 0 인 판에서 자원이 통째로 사라졌다.
			//   「지형 장식」과 「먹고사는 것」은 서로를 끌고 내려가면 안 된다.
			TowerDefenseMapParameters parameters = Parameters(7);
			parameters.RockSiteCount = 0;
			parameters.ObstacleDensity = 0f;

			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(parameters);

			Assert.AreEqual(0, layout.ObstacleCells.Count, "암반을 0 으로 줬는데 깔렸다.");
			Assert.Greater(layout.ResourceNodes.Count, 0, "암반이 없다고 자원까지 사라지면 안 된다.");
		}

		[Test]
		public void 코어_자리는_암반이_아니다()
		{
			// 코어가 암반에 묻히면 판이 시작부터 죽는다.
			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Parameters(42));

			Assert.IsFalse(layout.IsBlocked(layout.CoreCell), "코어가 암반 위에 앉았다.");
		}

		[Test]
		public void 출현_지점도_암반이_아니다()
		{
			// 출현 지점이 막히면 마수가 태어나자마자 굳는다(하네스가 이미 겪었다).
			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Parameters(99));

			Assert.Greater(layout.EnemySpawnPoints.Count, 0);
			foreach (Vector3 spawn in layout.EnemySpawnPoints)
				Assert.IsFalse(layout.IsBlocked(spawn), $"출현 지점 {spawn} 이 암반이다.");
		}

		[Test]
		public void 자원_노드는_코어에서_떨어져_있다()
		{
			// 코어 옆에 쌓으면 무위험 수입이 된다 — 개척의 긴장이 통째로 사라진다.
			TowerDefenseMapParameters parameters = Parameters(3);
			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(parameters);

			foreach (TowerDefenseResourceNodeSpot node in layout.ResourceNodes)
			{
				float distance = Vector2Int.Distance(node.Cell, layout.CoreCell);
				Assert.GreaterOrEqual(distance, parameters.NodeMinCoreDistance - 0.001f,
					$"노드 {node.Cell} 가 코어에 너무 붙었다({distance:0.0}칸).");
			}
		}

		[Test]
		public void 먼_노드일수록_많이_번다()
		{
			// 「멀리 나가면 번다」가 이 배수 하나로 성립한다 — 뒤집히면 개척할 이유가 사라진다.
			TowerDefenseMapLayout layout = TowerDefenseMapGenerator.Generate(Parameters(11));

			TowerDefenseResourceNodeSpot nearest = layout.ResourceNodes[0];
			TowerDefenseResourceNodeSpot farthest = layout.ResourceNodes[0];
			float nearestDistance = float.MaxValue;
			float farthestDistance = float.MinValue;

			foreach (TowerDefenseResourceNodeSpot node in layout.ResourceNodes)
			{
				float distance = Vector2Int.Distance(node.Cell, layout.CoreCell);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = node;
				}
				if (distance > farthestDistance)
				{
					farthestDistance = distance;
					farthest = node;
				}
			}

			Assert.GreaterOrEqual(farthest.IncomeMultiplier, nearest.IncomeMultiplier,
				"먼 노드가 더 적게 벌면 나갈 이유가 없다.");
		}
	}
}
