using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 고유 효과 '수선' 의 정본. 레벨업 몇 번마다 가장 덜 키운 장비가 조금 강해짐
	// 실 카드는 주기를 줄이고 (빈도), 한 번의 양을 늘림 (세기)
	public static class DollMendRules
	{
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
