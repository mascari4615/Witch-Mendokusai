using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 경계 없는 지형 회귀 — 「무한」이 성립하려면 ① 어느 좌표든 답이 나오고 ② 같은 좌표는 언제 물어도
	/// 같은 답이고 ③ 코어 주변은 반드시 열려 있고 ④ 벽이 통짜가 아니어서 길이 남는다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseInfiniteTerrainTests
	{
		private static readonly Vector2Int Core = Vector2Int.zero;

		private static TowerDefenseInfiniteTerrain Terrain(int seed = 1234)
		{
			return new TowerDefenseInfiniteTerrain(seed, Core);
		}

		[Test]
		public void 아주_먼_좌표도_답이_나온다()
		{
			// 판 밖이라는 것이 없다 — 이게 무한의 전부다.
			TowerDefenseInfiniteTerrain terrain = Terrain();

			Assert.DoesNotThrow(() => terrain.IsBlocked(new Vector2Int(1_000_000, -999_999)));
			Assert.DoesNotThrow(() => terrain.IsBlocked(new Vector2Int(int.MinValue / 2, int.MaxValue / 2)));
		}

		[Test]
		public void 같은_좌표는_언제_물어도_같은_답()
		{
			TowerDefenseInfiniteTerrain terrain = Terrain();
			Vector2Int cell = new Vector2Int(823, -417);

			bool first = terrain.IsBlocked(cell);
			for (int repeat = 0; repeat < 5; repeat++)
				Assert.AreEqual(first, terrain.IsBlocked(cell));
		}

		[Test]
		public void 씨앗이_다르면_지형이_달라진다()
		{
			TowerDefenseInfiniteTerrain a = Terrain(1);
			TowerDefenseInfiniteTerrain b = Terrain(2);

			int differences = 0;
			for (int x = -60; x < 60; x++)
			{
				for (int y = -60; y < 60; y++)
				{
					if (a.IsBlocked(new Vector2Int(x, y)) != b.IsBlocked(new Vector2Int(x, y)))
						differences++;
				}
			}

			Assert.Greater(differences, 100, "씨앗이 달라도 같은 판이면 매번 같은 개척이 된다.");
		}

		[Test]
		public void 코어_주변은_반드시_비어_있다()
		{
			// 시작하자마자 벽에 갇히는 판이 나오면 그 게임은 시작이 안 된다.
			TowerDefenseInfiniteTerrain terrain = Terrain();

			for (int x = -6; x <= 6; x++)
			{
				for (int y = -6; y <= 6; y++)
					Assert.IsFalse(terrain.IsBlocked(new Vector2Int(x, y)), $"코어 옆 {x},{y} 이 막혔다.");
			}
		}

		[Test]
		public void 벽이_판을_뒤덮지_않는다()
		{
			// 능선 위에만, 그것도 일부만 벽이 된다 — 통짜로 덮이면 길찾기 이전에 게임이 성립하지 않는다.
			TowerDefenseInfiniteTerrain terrain = Terrain();

			int blocked = 0;
			int total = 0;
			for (int x = 50; x < 150; x++)
			{
				for (int y = 50; y < 150; y++)
				{
					total++;
					if (terrain.IsBlocked(new Vector2Int(x, y)))
						blocked++;
				}
			}

			float ratio = blocked / (float)total;
			Assert.Less(ratio, 0.45f, "벽이 절반 가까이면 지나갈 자리가 없다.");
			Assert.Greater(ratio, 0.02f, "벽이 거의 없으면 길목이라는 것이 생기지 않는다.");
		}

		[Test]
		public void 자원_노드는_벽_위에_안_생긴다()
		{
			TowerDefenseInfiniteTerrain terrain = Terrain();

			for (int x = -200; x < 200; x += 3)
			{
				for (int y = -200; y < 200; y += 3)
				{
					Vector2Int cell = new Vector2Int(x, y);
					if (terrain.IsResourceNode(cell))
						Assert.IsFalse(terrain.IsBlocked(cell), $"{cell} 노드가 암반 위에 있다 — 채집을 못 세운다.");
				}
			}
		}

		[Test]
		public void 자원_노드가_아주_드물지도_흔하지도_않다()
		{
			TowerDefenseInfiniteTerrain terrain = Terrain();

			int nodes = 0;
			for (int x = 0; x < 200; x++)
			{
				for (int y = 0; y < 200; y++)
				{
					if (terrain.IsResourceNode(new Vector2Int(x, y)))
						nodes++;
				}
			}

			Assert.Greater(nodes, 20, "200×200 에 노드가 스무 개도 안 되면 개척할 곳이 없다.");
			Assert.Less(nodes, 2000, "노드가 흔하면 「어디로 넓힐까」가 선택이 아니다.");
		}

		[Test]
		public void 멀수록_벌이가_크되_상한이_있다()
		{
			TowerDefenseInfiniteTerrain terrain = Terrain();

			float near = terrain.IncomeMultiplierAt(new Vector2Int(5, 0), 1f, 3f, 100f);
			float far = terrain.IncomeMultiplierAt(new Vector2Int(80, 0), 1f, 3f, 100f);
			float absurd = terrain.IncomeMultiplierAt(new Vector2Int(100000, 0), 1f, 3f, 100f);

			Assert.Less(near, far);
			Assert.AreEqual(3f, absurd, 0.001f, "무한히 멀면 무한히 번다는 규칙은 게임이 아니다.");
		}
	}
}
