using System.Collections.Generic;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적이 세이브에 남기는 것. 업적 ID 마다 닿은 단계와 날짜.
	///
	/// 업적이 퀘스트 (Achievement 타입) 에 얹혀 있던 시절 세이브는 조각이 없음. <see cref="RestoreLegacy"/> 가
	/// 끝낸 퀘스트 ID 만 받아 두고, 어느 업적으로 옮길지는 <see cref="AchievementManager"/> 가 자산 (LegacyQuestId) 을 보고 정함
	/// </summary>
	public sealed class AchievementSaveSlice : IFeatureSaveSlice
	{
		public const string KEY = "achievement";

		[JsonIgnore]
		public string Key => KEY;

		public Dictionary<int, AchievementRecord> Records { get; set; } = new();

		/// <summary>메달 벽에 건 업적 ID, 건 순서 (<see cref="AchievementWall"/>)</summary>
		public List<int> WallPins { get; set; } = new();

		/// <summary>사용자가 벽을 한 번이라도 바꿨는지. 아니면 높은 단계부터 자동으로 채움</summary>
		public bool WallEdited { get; set; }

		/// <summary>옛 세이브의 끝낸 퀘스트 ID. 한 번 옮기면 비움. 세이브에 안 실림</summary>
		[JsonIgnore]
		public HashSet<int> LegacyCompletedQuestIds { get; private set; } = new();

		public void Reset()
		{
			Records = new();
			WallPins = new();
			WallEdited = false;
			LegacyCompletedQuestIds = new();
		}

		public string Capture() => JsonConvert.SerializeObject(this);

		public void Restore(string json)
		{
			AchievementSaveSlice loaded = JsonConvert.DeserializeObject<AchievementSaveSlice>(json);
			Records = loaded.Records ?? new();
			foreach (AchievementRecord record in Records.Values)
			{
				record.MigrateLegacyDate();
			}
			WallPins = loaded.WallPins ?? new();
			WallEdited = loaded.WallEdited;
		}

		public void RestoreLegacy(GameData saveData)
		{
			Records = new();
			WallPins = new();
			WallEdited = false;
			foreach (KeyValuePair<int, int> pair in saveData.questStates)
			{
				if ((QuestState)pair.Value == QuestState.Completed)
				{
					LegacyCompletedQuestIds.Add(pair.Key);
				}
			}
		}
	}
}
