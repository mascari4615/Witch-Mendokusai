using System.Collections.Generic;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터 기록이 세이브에 남기는 것. 조각이 없는 옛 세이브는 빈 기록에서 시작.
	/// 총 처치 수 (GameStat) 는 종을 몰라 옮길 수 없음
	/// </summary>
	public sealed class MonsterLogSaveSlice : IFeatureSaveSlice
	{
		public const string KEY = "monsterLog";

		[JsonIgnore]
		public string Key => KEY;

		public Dictionary<int, MonsterLogRecord> Records { get; set; } = new();

		public void Reset() => Records = new();

		public string Capture() => JsonConvert.SerializeObject(this);

		public void Restore(string json) => Records = JsonConvert.DeserializeObject<MonsterLogSaveSlice>(json).Records ?? new();

		public void RestoreLegacy(GameData saveData) => Records = new();
	}
}
