using System.Collections.Generic;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	/// <summary>
	/// 개척 갈래가 세이브에 남기는 것. 판 밖에 남는 기록과 재화와 뽑은 인형
	///
	/// 2026-09-06 전에는 DataManager 와 GameData 가 이 셋을 직접 보유. 옛 세이브는 <see cref="RestoreLegacy"/> 가 읽음
	/// 이어하기 (<see cref="Resume"/>) 는 세션 안에서만. 세이브에 안 실림
	/// </summary>
	public sealed class TowerDefenseSaveSlice : IFeatureSaveSlice
	{
		public const string KEY = "towerDefense";

		[JsonIgnore]
		public string Key => KEY;

		/// <summary>스테이지별 최고 점수. stageID -> 점수. 무한 모드라 이 기록이 곧 다시 할 이유</summary>
		public Dictionary<int, int> BestWave { get; set; } = new();

		/// <summary>유물. 버틴 만큼 받는 재화</summary>
		public int Relics { get; set; }

		/// <summary>유물로 뽑아 얻은 포탑 인형</summary>
		public List<int> UnlockedTowers { get; set; } = new();

		/// <summary>
		/// 개척 이어하기. 한 슬롯. 껐다 켜면 처음부터였던 것 (개선 목록 15번)
		/// 여러 슬롯을 두지 않는 이유: 판이 하나뿐인데 슬롯이 여럿이면 고르는 화면부터 필요, 그건 이어하기가 아니라 세이브 관리
		/// </summary>
		[JsonIgnore]
		public TowerDefenseSaveData Resume { get; set; }

		public void Reset()
		{
			BestWave = new();
			Relics = 0;
			UnlockedTowers = new();
			Resume = null;
		}

		public string Capture() => JsonConvert.SerializeObject(this);

		public void Restore(string json)
		{
			TowerDefenseSaveSlice loaded = JsonConvert.DeserializeObject<TowerDefenseSaveSlice>(json);
			BestWave = loaded.BestWave ?? new();
			Relics = loaded.Relics;
			UnlockedTowers = loaded.UnlockedTowers ?? new();
		}

		public void RestoreLegacy(GameData saveData)
		{
			// 옛 세이브에는 필드 자체가 없기도 함. null 은 빈 것으로
			BestWave = saveData.towerDefenseBestWave ?? new();
			Relics = saveData.towerDefenseRelics;
			UnlockedTowers = saveData.towerDefenseUnlockedTowers ?? new();
		}
	}
}
