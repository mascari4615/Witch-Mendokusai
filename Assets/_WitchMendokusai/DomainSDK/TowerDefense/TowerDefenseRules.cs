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
		// <= 0 = 무한(엔드리스, 코어가 부서질 때까지 웨이브가 영원히 이어짐 — 격파 수가 곧 점수).
		// > 0 = 해당 파 격퇴 시 승리(유한 스테이지). 별도 enum/모드 클래스 없이 이 값 하나가 분기 전부(TASK-WM-194).
		public int WaveCount;
		public float PrepareSeconds;    // 웨이브 사이 건설 시간.
		public int StartingResource;    // 시작 자원.
		public int BaseWaveIncome;      // 웨이브 격퇴 기본 수입.
		public int IncomePerHarvester;  // 가동 중인 채집건물 1개당 추가 수입 = 개척 보상.
		public int FirstWaveEnemyCount; // 1파 적 수.
		public int EnemyCountGrowth;    // 파당 적 증가량 = escalation(엔드리스에서 난이도가 영원히 오르는 유일한 노브).

		// 마수 1기 격파 보상. 웨이브 정산만 있으면 교전 중엔 아무 일도 안 일어나 「잡는 맛」이 0 이다
		// (사용자 실증: "재미가 없네"). 격파 즉시 자원이 들어와야 조준·배치가 순간순간 보상받는다.
		public int BountyPerKill;

		// 코어를 때려 부수는 대신 「목표에 닿은 마수 수」로 진다 = 장르 표준의 유출(leak)제.
		// 코어 체력제는 「아직 얼마 남았나」 하나만 긴장인데, 유출제는 *한 마리라도 새면 아프다* 가 되어
		// 길목 하나가 뚫리는 순간의 무게가 완전히 달라진다. 0 이하면 유출제 미사용(옛 코어 체력제).
		public int StartingLives;

		// 바깥 노드 채집 인형 1기당 정산 시 들어오는 정수. 강화 전용 재화라 「멀리 나가야 강해진다」가 성립한다.
		public int EssencePerHarvester;

		/// <summary> WaveCount 가 무한 스테이지 센티널(0 이하)인지. </summary>
		public bool IsEndless => WaveCount <= 0;

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
