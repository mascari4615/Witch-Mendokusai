using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척 최고 기록(TASK-WM-194) — 스테이지별 「최고 도달 웨이브」 순수 규칙.
	///
	/// 개척은 무한 모드다(TowerDefenseRules.IsEndless): 승리가 없고 코어가 부서질 때까지 웨이브가 이어지며
	/// **버틴 웨이브 수가 곧 점수**다. 그러면 기록이 없는 순간 게임이 성립하지 않는다 — 끝나도 남는 게 없고,
	/// 다음 판이 지난 판보다 나은지 알 방법이 없으니 「다시 도전」이 의미를 못 갖는다.
	///
	/// 저장 매체(GameData.towerDefenseBestWave)와 분리된 순수 함수로 둔다 — 규칙만 EditMode 로 검증 가능.
	/// </summary>
	public static class TowerDefenseRecord
	{
		/// <summary> 해당 스테이지 최고 기록. 기록 없으면 0. </summary>
		public static int Best(IReadOnlyDictionary<int, int> records, int stageId)
		{
			if (records == null)
				return 0;
			return records.TryGetValue(stageId, out int best) ? best : 0;
		}

		/// <summary>
		/// 이번 판 결과 제출. 기록을 넘겼으면 갱신하고 true.
		/// best 에는 제출 후의 최고 기록이 담긴다(갱신 실패해도 기존 최고를 그대로 알려준다).
		/// </summary>
		public static bool Submit(Dictionary<int, int> records, int stageId, int wavesCleared, out int best)
		{
			best = Best(records, stageId);
			if (records == null || wavesCleared <= best)
				return false;

			best = wavesCleared;
			records[stageId] = wavesCleared;
			return true;
		}
	}
}
