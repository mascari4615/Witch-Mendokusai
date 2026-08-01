using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 웨이브 구성 규칙 회귀 — 「이번 파에 뭐가 몇 마리」. 화면 예고와 실제 스폰이 같은 함수를 쓰므로
	/// 이 규칙이 흔들리면 예고가 곧 거짓말이 된다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseWaveComposerTests
	{
		// 0번 = 처음부터, 1번 = 2파(index 1)부터, 2번 = 3파(index 2)부터.
		private static readonly int[] UnlockWaves = { 0, 1, 2 };
		private static readonly int[] Weights = { 5, 4, 2 };

		private static List<int> Compose(int waveIndex, int enemyCount)
		{
			List<int> result = new();
			TowerDefenseWaveComposer.Compose(UnlockWaves, Weights, waveIndex, enemyCount, result);
			return result;
		}

		[Test]
		public void 총_마리수는_항상_요청한_수와_같다()
		{
			for (int wave = 0; wave < 8; wave++)
			{
				for (int count = 1; count <= 20; count++)
					Assert.AreEqual(count, Compose(wave, count).Count, $"wave={wave} count={count}");
			}
		}

		[Test]
		public void 해금_전_종류는_절대_안_나온다()
		{
			CollectionAssert.DoesNotContain(Compose(0, 12), 1);
			CollectionAssert.DoesNotContain(Compose(0, 12), 2);
			CollectionAssert.DoesNotContain(Compose(1, 12), 2);
		}

		[Test]
		public void 해금된_웨이브부터는_등장한다()
		{
			CollectionAssert.Contains(Compose(1, 12), 1);
			CollectionAssert.Contains(Compose(2, 12), 2);
		}

		[Test]
		public void 같은_웨이브는_항상_같은_구성이다()
		{
			// 대비가 성립하려면 결정론이어야 한다 — 무작위면 준비가 운으로 무효화된다.
			CollectionAssert.AreEqual(Compose(3, 11), Compose(3, 11));
		}

		[Test]
		public void 비중이_큰_종류가_더_많이_나온다()
		{
			List<int> composition = Compose(2, 22); // 비중 5:4:2 → 10:8:4
			int common = composition.FindAll(index => index == 0).Count;
			int fast = composition.FindAll(index => index == 1).Count;
			int tank = composition.FindAll(index => index == 2).Count;

			Assert.Greater(common, fast);
			Assert.Greater(fast, tank);
			Assert.AreEqual(22, common + fast + tank);
		}

		[Test]
		public void 한_웨이브_안에서_종류가_섞여_나온다()
		{
			// 몰아서 내보내면 "앞은 전부 방패, 뒤는 전부 돌진" 이 돼 섞이는 맛이 사라진다.
			List<int> composition = Compose(2, 12);
			bool changed = false;
			for (int index = 1; index < composition.Count; index++)
			{
				if (composition[index] != composition[index - 1])
				{
					changed = true;
					break;
				}
			}
			Assert.IsTrue(changed, "스폰 순서가 종류별로 통째 몰려 있다.");
		}

		[Test]
		public void 종류가_없으면_기본_한_종류로_채운다()
		{
			List<int> result = new();
			TowerDefenseWaveComposer.Compose(null, null, 3, 5, result);

			Assert.AreEqual(5, result.Count, "마수 0마리 웨이브 = 진행 정지이므로 절대 나오면 안 된다.");
			CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, result);
		}

		[Test]
		public void 집계는_종류별_마리수를_센다()
		{
			List<int> composition = Compose(2, 22);
			int[] counts = new int[3];

			TowerDefenseWaveComposer.CountByArchetype(composition, 3, counts);

			Assert.AreEqual(22, counts[0] + counts[1] + counts[2]);
			Assert.AreEqual(composition.FindAll(index => index == 2).Count, counts[2]);
		}
	}
}
