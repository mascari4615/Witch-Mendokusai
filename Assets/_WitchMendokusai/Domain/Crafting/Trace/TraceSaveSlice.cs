using System.Collections.Generic;
using Newtonsoft.Json;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	/// <summary>
	/// 자취 연구가 세이브에 남기는 것. 연구 기록 하나
	/// 도구별 처치 수는 이번 판에만 씀. 세이브에 안 실림
	/// </summary>
	public sealed class TraceSaveSlice : IFeatureSaveSlice
	{
		public const string KEY = "trace";

		[JsonIgnore]
		public string Key => KEY;

		public TraceNotebook Notebook { get; set; } = new();

		/// <summary>이번 판의 도구별 처치 수. 도구를 모르는 처치 (상태이상 틱, 화면 지우기, 웅덩이) 는 NO_DATA_ID 칸</summary>
		[JsonIgnore]
		public Dictionary<int, int> RunKills { get; } = new();

		public void BeginRun() => RunKills.Clear();

		public void CountKill(int equipmentDataId)
		{
			RunKills.TryGetValue(equipmentDataId, out int kills);
			RunKills[equipmentDataId] = kills + 1;
		}

		/// <summary>이번 판에 가장 많이 잡은 도구의 자취. 보스 재료에 씀</summary>
		public TraceType MainTraceOfRun() => TraceRules.MainTrace(RunKills, TraceSource.OfTool);

		public void Reset()
		{
			Notebook = new();
			RunKills.Clear();
		}

		public string Capture() => JsonConvert.SerializeObject(this);

		public void Restore(string json)
		{
			TraceSaveSlice loaded = JsonConvert.DeserializeObject<TraceSaveSlice>(json);
			Notebook = loaded.Notebook ?? new();
			Notebook.Counts ??= new();
			RunKills.Clear();
		}

		// 조각이 생기기 전 세이브에는 기록이 없음
		public void RestoreLegacy(GameData saveData) => Reset();
	}
}
