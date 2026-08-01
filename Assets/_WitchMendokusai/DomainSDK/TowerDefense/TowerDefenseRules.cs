using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 한 스테이지의 진행·경제 수치. 순수 데이터(DomainSDK) — 전부 SO 인스펙터로 노출되는
	/// 밸런싱 노브(수치 노출 룰: 하드코딩 0). TowerDefenseCore 가 이 값만 보고 규칙을 돌린다.
	/// </summary>
	[Serializable]
	public struct TowerDefenseRules
	{
		public int WaveCount;           // 이만큼 격퇴하면 승리.
		public float PrepareSeconds;    // 웨이브 사이 건설 시간.
		public int StartingResource;    // 시작 자원.
		public int BaseWaveIncome;      // 웨이브 격퇴 기본 수입.
		public int IncomePerHarvester;  // 가동 중인 채집건물 1개당 추가 수입 = 개척 보상.
		public int FirstWaveEnemyCount; // 1파 적 수.
		public int EnemyCountGrowth;    // 파당 적 증가량 = escalation.

		/// <summary> waveIndex(0-based) 파의 적 수. </summary>
		public int EnemiesInWave(int waveIndex)
		{
			int count = FirstWaveEnemyCount + waveIndex * EnemyCountGrowth;
			return count < 1 ? 1 : count;
		}

		/// <summary> 채집건물 harvesterCount 개일 때 웨이브 격퇴 수입. </summary>
		public int IncomeFor(int harvesterCount)
		{
			return BaseWaveIncome + harvesterCount * IncomePerHarvester;
		}
	}
}
