using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 매치의 승패 규칙(모드)을 정의하는 SO 베이스. v1 = BrawlArenaMode(전멸) 유일.
	/// 미래(6동기) = ObjectiveArenaMode(점령)·LaneArenaMode(넥서스 파괴) 서브클래싱으로 코어 무변경 확장.
	/// 데이터(:DataSO) 라 모드별 밸런싱 노브를 SO 인스펙터로 노출(수치 노출 룰).
	/// </summary>
	public abstract class ArenaModeSO : DataSO
	{
		/// <summary> CheckVictory 가 승자 없음(진행 중 또는 무승부)을 표현하는 TeamId 센티넬. </summary>
		public const int NO_WINNER = -1;

		/// <summary>
		/// 현재 팀 상태로 매치 종료 여부 판정. 반환 true = 종료(winnerTeamId = 승리 팀, 무승부면 NO_WINNER),
		/// false = 진행 중(winnerTeamId = NO_WINNER). 매 틱 호출되므로 부작용 없는 순수 판정만.
		/// </summary>
		public abstract bool CheckVictory(IReadOnlyList<ArenaTeam> teams, out int winnerTeamId);
	}
}
