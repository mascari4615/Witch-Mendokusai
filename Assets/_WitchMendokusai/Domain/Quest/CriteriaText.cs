using UnityEngine;

namespace WitchMendokusai
{
	// 퀘스트 조건을 화면 글로. 이름과 값의 단위 (시간 기록은 분) 를 한곳에서 정해 별 밑 글과 상세가 같은 말을 쓰게
	public static class CriteriaText
	{
		private const int SECONDS_PER_MINUTE = 60;

		public static string NameOf(Criteria criteria) => criteria switch
		{
			ItemCountCriteria itemCount => SOHelper.GetItemData(itemCount.ItemID)?.Name ?? "아이템",
			GameStatCriteria gameStat => StatDisplayName.Get(gameStat.Type),
			StatCriteria stat => StatDisplayName.Get(stat.Type),
			IntCriteria intCriteria => intCriteria.IntVariable.Name,
			_ => "조건",
		};

		// 누적 기록은 목표를 넘어 쌓이므로 표시는 목표에서 멈춤. 목표가 없는 조건은 빈 글
		public static string ProgressOf(RuntimeCriteria criteria)
		{
			int target = criteria.GetTargetValue();
			if (target <= 0)
			{
				return string.Empty;
			}
			int current = Mathf.Min(criteria.GetCurValue(), target);
			return $"{ValueOf(criteria.Criteria, current)} / {ValueOf(criteria.Criteria, target)}";
		}

		private static string ValueOf(Criteria criteria, int value)
		{
			if (IsSeconds(criteria))
			{
				return $"{value / SECONDS_PER_MINUTE}분";
			}
			return value.ToString();
		}

		private static bool IsSeconds(Criteria criteria) =>
			criteria is GameStatCriteria { Type: GameStatType.TOTAL_DUNGEON_TIME }
			|| criteria is DungeonStatCriteria { Type: DungeonStatType.DUNGEON_TIME };
	}
}
