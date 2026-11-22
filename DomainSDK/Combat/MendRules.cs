using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 수선의 정본. 반짇고리 실 카드의 효과 (2026-11-22 21:03 고유 효과에서 옮김)
	// 레벨업 몇 번마다 가장 덜 키운 장비가 조금 강해짐. 실 한 타래 첫 장이 수선을 켜고 더 받으면 주기가 줄고 (빈도), 튼튼한 바느질이 양을 늘림 (세기)
	public static class MendRules
	{
		public static bool IsActive(int frequencyCards) => frequencyCards > 0;

		// 첫 장은 켜기만, 둘째 장부터 한 장마다 주기 한 칸 감소
		public static int IntervalFromCards(int baseInterval, int frequencyCards, int minInterval) => Interval(baseInterval, frequencyCards - 1, minInterval);

		// 수선 주기 (레벨업 횟수). 줄여도 최소 주기 아래로는 안 감
		public static int Interval(int baseInterval, int reduction, int minInterval)
		{
			int floor = Math.Max(1, minInterval);
			return Math.Max(floor, baseInterval - Math.Max(0, reduction));
		}

		// levelUps 는 이번 판 레벨업 누적 (1 부터)
		public static bool TriggersAt(int levelUps, int interval) => levelUps > 0 && interval > 0 && levelUps % interval == 0;

		public static int Amount(int baseAmount, int bonus) => Math.Max(0, baseAmount + bonus);

		// 받은 카드 수가 가장 적은 후보. 같으면 앞 번호 (전용 무기가 0 번). 후보가 없으면 -1
		public static int PickLeastGrown(IReadOnlyList<int> picksPerCandidate)
		{
			int picked = -1;
			for (int i = 0; i < picksPerCandidate.Count; i++)
			{
				if (picksPerCandidate[i] < 0)
					continue;
				if (picked < 0 || picksPerCandidate[i] < picksPerCandidate[picked])
					picked = i;
			}
			return picked;
		}
	}
}
