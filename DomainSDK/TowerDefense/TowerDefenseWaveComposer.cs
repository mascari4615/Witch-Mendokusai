using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 웨이브 구성 규칙 — 「이번 파에 어떤 마수가 몇 마리 오는가」 (TASK-WM-194).
	///
	/// ★ 왜 필요한가: 마수가 한 종류면 매 웨이브의 판단이 똑같아서 몇 판 만에 지루해진다
	///   (사용자 실증: "재미가 없네"). 종류가 섞여야 「이번엔 어디에 몇 개」가 매번 달라진다.
	///
	/// ★ 왜 결정론인가: 같은 웨이브는 항상 같은 구성이어야 *대비*가 성립한다. 무작위면 준비가
	///   운으로 무효화되고, 화면에 「다음 웨이브 예고」를 띄우는 것도 거짓말이 된다.
	///   최대잉여법(largest remainder)으로 비중을 정수 마리수로 나누므로 총합이 정확히 맞는다.
	///
	/// 순수 정적 — Unity 타입 0, 전역 RNG 0. EditMode 로 전량 검증 가능.
	/// </summary>
	public static class TowerDefenseWaveComposer
	{
		/// <summary>
		/// waveIndex(0-based) 파의 마수 구성. result 에 archetype 인덱스가 스폰 순서대로 담긴다.
		/// unlockWaves[i] 초과 웨이브부터 등장, weights[i] 는 등장 비중(0 이하 = 미등장).
		/// 해금된 종류가 하나도 없으면 0번을 전부 채운다(마수 0마리 웨이브 = 진행 정지이므로 절대 금지).
		/// </summary>
		public static void Compose(
			IReadOnlyList<int> unlockWaves,
			IReadOnlyList<int> weights,
			int waveIndex,
			int enemyCount,
			List<int> result)
		{
			result.Clear();
			if (enemyCount <= 0)
				return;

			int archetypeCount = unlockWaves != null && weights != null
				? (unlockWaves.Count < weights.Count ? unlockWaves.Count : weights.Count)
				: 0;

			if (archetypeCount <= 0)
			{
				FillSingle(result, 0, enemyCount);
				return;
			}

			List<int> eligible = new();
			int totalWeight = 0;
			for (int index = 0; index < archetypeCount; index++)
			{
				if (unlockWaves[index] > waveIndex || weights[index] <= 0)
					continue;
				eligible.Add(index);
				totalWeight += weights[index];
			}

			if (eligible.Count == 0 || totalWeight <= 0)
			{
				FillSingle(result, 0, enemyCount);
				return;
			}

			// 1단계 — 비중대로 나눈 몫(내림)을 먼저 배정.
			int[] counts = new int[eligible.Count];
			double[] remainders = new double[eligible.Count];
			int assigned = 0;
			for (int slot = 0; slot < eligible.Count; slot++)
			{
				double exact = (double)enemyCount * weights[eligible[slot]] / totalWeight;
				counts[slot] = (int)exact;
				remainders[slot] = exact - counts[slot];
				assigned += counts[slot];
			}

			// 2단계 — 남은 마리수를 소수부가 큰 순서로 한 마리씩(동률이면 낮은 인덱스 우선 = 결정론).
			for (int remaining = enemyCount - assigned; remaining > 0; remaining--)
			{
				int bestSlot = 0;
				double bestRemainder = -1.0;
				for (int slot = 0; slot < eligible.Count; slot++)
				{
					if (remainders[slot] <= bestRemainder)
						continue;
					bestRemainder = remainders[slot];
					bestSlot = slot;
				}
				counts[bestSlot]++;
				remainders[bestSlot] = -1.0; // 같은 슬롯이 잉여를 독식하지 않게 소진 처리.
			}

			// 3단계 — 스폰 순서를 섞는다. 몰아서 내보내면 "앞은 전부 방패, 뒤는 전부 돌진" 이 돼
			//         한 웨이브 안에서 종류가 섞이는 맛이 사라진다. 남은 수가 많은 종류부터 한 마리씩.
			int totalRemaining = enemyCount;
			while (totalRemaining > 0)
			{
				int bestSlot = -1;
				for (int slot = 0; slot < eligible.Count; slot++)
				{
					if (counts[slot] <= 0)
						continue;
					if (bestSlot < 0 || counts[slot] > counts[bestSlot])
						bestSlot = slot;
				}
				if (bestSlot < 0)
					break;

				result.Add(eligible[bestSlot]);
				counts[bestSlot]--;
				totalRemaining--;
			}
		}

		/// <summary> 종류별 마리수 집계 — 화면 예고("다음 웨이브: 돌진 3 · 방패 1")용. </summary>
		public static void CountByArchetype(IReadOnlyList<int> composition, int archetypeCount, int[] counts)
		{
			for (int index = 0; index < counts.Length; index++)
				counts[index] = 0;

			if (composition == null)
				return;

			foreach (int archetypeIndex in composition)
			{
				if (archetypeIndex >= 0 && archetypeIndex < counts.Length && archetypeIndex < archetypeCount)
					counts[archetypeIndex]++;
			}
		}

		private static void FillSingle(List<int> result, int archetypeIndex, int enemyCount)
		{
			for (int index = 0; index < enemyCount; index++)
				result.Add(archetypeIndex);
		}
	}
}
