using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 흩뿌린 서식지 — 「코어 옆에 안 붙는다 · 뭉치지 않는다 · 같은 씨앗이면 같은 자리」 (TASK-WM-194).
	///
	/// ★ 셋 다 판의 공정함을 정한다. 코어 옆에 붙으면 시작하자마자 죽고, 뭉치면 판 한쪽만 위험하고,
	///   자리가 흔들리면 저장·복원 때 서식지가 딴 데로 옮겨간다. 전부 화면 없이 잴 수 있다.
	/// </summary>
	public class TowerDefenseLairPlacementTests
	{
		private static List<Vector2Int> Choose(int seed, int count, float minCoreDistance, float minSpacing,
			System.Func<Vector2Int, bool> blocked = null)
		{
			List<Vector2Int> result = new();
			TowerDefenseLairPlacement.Choose(seed, 40, 40, new Vector2Int(20, 20), blocked,
				count, minCoreDistance, minSpacing, result);
			return result;
		}

		[Test]
		public void 코어_옆에는_안_선다()
		{
			List<Vector2Int> lairs = Choose(seed: 7, count: 8, minCoreDistance: 10f, minSpacing: 4f);

			Assert.IsNotEmpty(lairs);
			foreach (Vector2Int lair in lairs)
			{
				Assert.GreaterOrEqual(Vector2Int.Distance(lair, new Vector2Int(20, 20)), 10f,
					"코어 옆에 서식지가 붙으면 시작하자마자 진다 — 판이 공정하지 않다.");
			}
		}

		[Test]
		public void 서로_뭉치지_않는다()
		{
			List<Vector2Int> lairs = Choose(seed: 3, count: 10, minCoreDistance: 8f, minSpacing: 6f);

			for (int left = 0; left < lairs.Count; left++)
			{
				for (int right = left + 1; right < lairs.Count; right++)
				{
					Assert.GreaterOrEqual(Vector2Int.Distance(lairs[left], lairs[right]), 6f,
						"뭉치면 판 한쪽만 위험해져 「어느 쪽으로 넓힐까」가 죽는다.");
				}
			}
		}

		[Test]
		public void 같은_씨앗이면_같은_자리다()
		{
			CollectionAssert.AreEqual(
				Choose(seed: 11, count: 6, minCoreDistance: 8f, minSpacing: 5f),
				Choose(seed: 11, count: 6, minCoreDistance: 8f, minSpacing: 5f),
				"자리가 흔들리면 이어할 때 서식지가 딴 데로 옮겨간다.");
		}

		[Test]
		public void 씨앗이_다르면_다른_판이다()
		{
			List<Vector2Int> first = Choose(seed: 1, count: 6, minCoreDistance: 8f, minSpacing: 5f);
			List<Vector2Int> second = Choose(seed: 2, count: 6, minCoreDistance: 8f, minSpacing: 5f);

			Assert.AreNotEqual(first, second, "씨앗이 달라도 같은 판이면 다시 할 이유가 없다.");
		}

		[Test]
		public void 막힌_칸에는_안_선다()
		{
			// 암반 위에 서식지가 서면 그 마수는 시작부터 갈 수 없는 자리에 굳는다.
			List<Vector2Int> lairs = Choose(seed: 5, count: 8, minCoreDistance: 8f, minSpacing: 4f,
				blocked: cell => cell.x % 2 == 0);

			Assert.IsNotEmpty(lairs);
			foreach (Vector2Int lair in lairs)
				Assert.AreNotEqual(0, lair.x % 2, "막힌 칸을 골랐다.");
		}

		[Test]
		public void 자리가_모자라면_억지로_안_채운다()
		{
			// 규칙을 못 지키면서까지 개수를 맞추면 코어 옆·겹침이 도로 생긴다.
			List<Vector2Int> lairs = Choose(seed: 9, count: 500, minCoreDistance: 10f, minSpacing: 12f);

			Assert.Less(lairs.Count, 500, "다 채웠다면 규칙 하나를 깬 것이다.");
			for (int left = 0; left < lairs.Count; left++)
			{
				for (int right = left + 1; right < lairs.Count; right++)
					Assert.GreaterOrEqual(Vector2Int.Distance(lairs[left], lairs[right]), 12f);
			}
		}

		[Test]
		public void 판_전체에_퍼진다()
		{
			// 훑는 순서가 나쁘면 한쪽 구석만 채워진다 — 그러면 반대편으로 넓히는 데 아무 위험이 없다.
			List<Vector2Int> lairs = Choose(seed: 4, count: 12, minCoreDistance: 8f, minSpacing: 4f);

			bool left = false, right = false, down = false, up = false;
			foreach (Vector2Int lair in lairs)
			{
				left |= lair.x < 20;
				right |= lair.x > 20;
				down |= lair.y < 20;
				up |= lair.y > 20;
			}

			Assert.IsTrue(left && right && down && up, "네 방향 모두에 서식지가 있어야 판 전체가 위험하다.");
		}

		[Test]
		public void 가까이_가야_깨어난다()
		{
			Vector3 lair = new(10f, 0f, 0f);
			List<Vector3> far = new() { new Vector3(0f, 0f, 0f) };
			List<Vector3> near = new() { new Vector3(7f, 0f, 0f) };

			Assert.IsFalse(TowerDefenseLairPlacement.ShouldWake(lair, far, 5f),
				"멀리서도 깨면 넓히는 것이 위험이 아니라 그냥 파도가 하나 더 있는 것이다.");
			Assert.IsTrue(TowerDefenseLairPlacement.ShouldWake(lair, near, 5f),
				"가까이 갔는데 안 깨면 판을 장식하는 조형물이다.");
		}
	}
}
