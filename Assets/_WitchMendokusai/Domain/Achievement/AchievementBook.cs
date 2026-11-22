using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>업적 하나의 달성 기록. 세이브에 그대로 실림</summary>
	public sealed class AchievementRecord
	{
		public int Tier { get; set; }

		/// <summary>마지막 단계에 닿은 날 (yyyy-MM-dd)</summary>
		public string UnlockedOn { get; set; }
	}

	/// <summary>
	/// 게임 업적 장부. 업적 ID 마다 닿은 단계와 날짜. 단계는 오르기만 함.
	/// 플랫폼은 <see cref="IAchievementLedger"/> 로 읽기만. 엔진 없음
	/// </summary>
	public sealed class AchievementBook : IAchievementLedger
	{
		private readonly Dictionary<int, AchievementRecord> records;

		/// <param name="records">세이브 조각이 들고 있는 사전. 복사하지 않고 그대로 써서 저장할 때 따로 옮길 것이 없음</param>
		public AchievementBook(Dictionary<int, AchievementRecord> records)
		{
			this.records = records;
		}

		public int TierOf(int achievementId) => records.TryGetValue(achievementId, out AchievementRecord record) ? record.Tier : 0;

		public string UnlockedOnOf(int achievementId) => records.TryGetValue(achievementId, out AchievementRecord record) ? record.UnlockedOn : null;

		/// <summary>단계를 올림. 지금보다 높을 때만 바뀌고, 바뀌기 전 단계를 돌려줌 (안 바뀌면 지금 단계)</summary>
		public int Advance(int achievementId, int tier, string today)
		{
			int before = TierOf(achievementId);
			if (tier <= before)
			{
				return before;
			}
			records[achievementId] = new AchievementRecord { Tier = tier, UnlockedOn = today };
			return before;
		}

		/// <summary>1단계 이상 닿은 업적 수</summary>
		public int UnlockedCount
		{
			get
			{
				int count = 0;
				foreach (AchievementRecord record in records.Values)
				{
					if (record.Tier > 0)
					{
						count++;
					}
				}
				return count;
			}
		}

		/// <summary>지금 단계가 정확히 tier 인 업적 수 (머리줄의 단계별 개수)</summary>
		public int CountAtTier(int tier)
		{
			int count = 0;
			foreach (AchievementRecord record in records.Values)
			{
				if (record.Tier == tier)
				{
					count++;
				}
			}
			return count;
		}

		public IReadOnlyCollection<AchievementStep> UnlockedSteps
		{
			get
			{
				List<AchievementStep> steps = new();
				foreach (KeyValuePair<int, AchievementRecord> pair in records)
				{
					for (int tier = 1; tier <= pair.Value.Tier; tier++)
					{
						steps.Add(new AchievementStep(pair.Key, tier));
					}
				}
				return steps;
			}
		}
	}
}
