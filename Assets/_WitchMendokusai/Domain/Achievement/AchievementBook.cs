using System.Collections.Generic;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	/// <summary>업적 하나의 달성 기록. 세이브에 그대로 실림</summary>
	public sealed class AchievementRecord
	{
		public int Tier { get; set; }

		/// <summary>단계마다 닿은 날 (yyyy-MM-dd). 0번이 1단계. 날짜를 모르는 단계는 null (단계별 날짜가 생기기 전 세이브)</summary>
		public List<string> TierDates { get; set; } = new();

		/// <summary>단계별 날짜가 생기기 전 세이브의 "마지막 단계에 닿은 날". 읽을 때 TierDates 로 옮기고 비움 (<see cref="MigrateLegacyDate"/>)</summary>
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string UnlockedOn { get; set; }

		/// <summary>옛 날짜 하나를 지금 단계의 날짜로. 아래 단계 날짜는 모름 (null)</summary>
		public void MigrateLegacyDate()
		{
			TierDates ??= new List<string>();
			if (UnlockedOn == null)
			{
				return;
			}
			if (TierDates.Count == 0)
			{
				for (int tier = 1; tier <= Tier; tier++)
				{
					TierDates.Add(tier == Tier ? UnlockedOn : null);
				}
			}
			UnlockedOn = null;
		}
	}

	/// <summary>
	/// 게임 업적 장부. 업적 ID 마다 닿은 단계와 단계별 날짜. 단계는 오르기만 함.
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

		/// <summary>그 단계에 닿은 날. 아직 못 닿았거나 날짜를 모르면 null</summary>
		public string DateOf(int achievementId, int tier)
		{
			if (tier <= 0 || records.TryGetValue(achievementId, out AchievementRecord record) == false || tier > record.TierDates.Count)
			{
				return null;
			}
			return record.TierDates[tier - 1];
		}

		/// <summary>지금 단계에 닿은 날 (최근 달성 목록). 모르면 null</summary>
		public string LatestDateOf(int achievementId) => DateOf(achievementId, TierOf(achievementId));

		/// <summary>단계를 올림. 지금보다 높을 때만 바뀌고, 새로 닿은 단계마다 오늘 날짜. 바뀌기 전 단계를 돌려줌 (안 바뀌면 지금 단계)</summary>
		public int Advance(int achievementId, int tier, string today)
		{
			int before = TierOf(achievementId);
			if (tier <= before)
			{
				return before;
			}
			if (records.TryGetValue(achievementId, out AchievementRecord record) == false)
			{
				record = new AchievementRecord();
				records[achievementId] = record;
			}
			while (record.TierDates.Count < before)
			{
				record.TierDates.Add(null);
			}
			for (int reached = before + 1; reached <= tier; reached++)
			{
				record.TierDates.Add(today);
			}
			record.Tier = tier;
			return before;
		}

		/// <summary>1단계 이상 닿은 업적 수</summary>
		public int UnlockedCount => CountAtLeast(1);

		/// <summary>지금 단계가 tier 이상인 업적 수 (메달 벽의 단계별 메달 수)</summary>
		public int CountAtLeast(int tier)
		{
			int count = 0;
			foreach (AchievementRecord record in records.Values)
			{
				if (record.Tier >= tier)
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
