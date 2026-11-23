using UnityEngine;

namespace WitchMendokusai
{
	// 퀘스트 조건을 화면 글로. 이름과 값의 단위 (시간 기록은 분) 를 한곳에서 정해 별 밑 글과 상세가 같은 말을 쓰게
	public static class CriteriaText
	{
		private const int SECONDS_PER_MINUTE = 60;
		private const float PERCENT = 100f;

		public static string NameOf(Criteria criteria) => criteria switch
		{
			ItemCountCriteria itemCount => SOHelper.GetItemData(itemCount.ItemID)?.Name ?? "아이템",
			GameStatCriteria gameStat => StatDisplayName.Get(gameStat.Type),
			StatCriteria stat => StatDisplayName.Get(stat.Type),
			DungeonStatCriteria { Type: DungeonStatType.DUNGEON_TIME } => "던전 시간",
			DungeonStatCriteria => "던전 기록",
			IntCriteria intCriteria => intCriteria.IntVariable.Name,
			_ => "조건",
		};

		// 조건 한 줄. 이름과 진행 값 (예: 이름  3 / 20). 마도서 상세와 결과창이 같은 줄을 씀
		public static string LineOf(RuntimeCriteria criteria)
		{
			string name = NameOf(criteria.Criteria);
			string progress = ProgressOf(criteria);
			return string.IsNullOrEmpty(progress) ? name : $"{name}  {progress}";
		}

		// 누적 기록은 목표를 넘어 쌓이므로 표시는 목표에서 멈춤. 목표 값이 없는 조건 (대화 등) 은 진행률
		public static string ProgressOf(RuntimeCriteria criteria)
		{
			int target = criteria.GetTargetValue();
			if (target <= 0)
			{
				return $"{Mathf.Clamp01(criteria.GetProgress()) * PERCENT:0}%";
			}
			int current = Mathf.Min(criteria.GetCurValue(), target);
			if (IsSeconds(criteria.Criteria))
			{
				return TimeOf(current, target);
			}
			return $"{current} / {target}";
		}

		// 1분 아래 목표는 초로. 분으로 쓸 때 지금 값은 내림, 목표는 올림 (끝나기 전에 같은 숫자로 보이지 않게)
		private static string TimeOf(int currentSeconds, int targetSeconds)
		{
			if (targetSeconds < SECONDS_PER_MINUTE)
			{
				return $"{currentSeconds}초 / {targetSeconds}초";
			}
			int targetMinutes = Mathf.CeilToInt((float)targetSeconds / SECONDS_PER_MINUTE);
			int currentMinutes = currentSeconds >= targetSeconds ? targetMinutes : Mathf.Min(currentSeconds / SECONDS_PER_MINUTE, targetMinutes - 1);
			return $"{currentMinutes}분 / {targetMinutes}분";
		}

		private static bool IsSeconds(Criteria criteria) =>
			criteria is GameStatCriteria { Type: GameStatType.TOTAL_DUNGEON_TIME }
			|| criteria is DungeonStatCriteria { Type: DungeonStatType.DUNGEON_TIME };
	}
}
