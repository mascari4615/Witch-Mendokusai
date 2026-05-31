using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 매치 진행 브레인 — 팀 상태를 모드 규칙(ArenaModeSO)으로 매 틱 폴링해 승패를 *단 한 번* 확정.
	/// 순수(MonoBehaviour/물리/이벤트버스 0): 스폰·드라이버 부착·종료 이벤트 발행은 ArenaMatch(Mono, 콘텐츠 슬라이스)가,
	/// 승패 판정 + 종료-once 멱등 + 승자 보관은 본 코어가. EditMode 에서 fake 로 검증.
	/// </summary>
	public class ArenaMatchCore
	{
		private readonly IReadOnlyList<ArenaTeam> teams;
		private readonly ArenaModeSO mode;

		public bool IsConcluded { get; private set; }
		public int WinnerTeamId { get; private set; } = ArenaModeSO.NO_WINNER;

		public ArenaMatchCore(IReadOnlyList<ArenaTeam> teams, ArenaModeSO mode)
		{
			this.teams = teams;
			this.mode = mode;
		}

		/// <summary>
		/// 한 틱 평가. 매치 종료가 *이번 호출에서 처음* 확정되면 true(호출자 = 종료 이벤트 발행 시점).
		/// 진행 중이거나 이미 종료된 뒤면 false. 종료는 멱등 — 첫 확정에서만 true, 이후는 영구 false.
		/// </summary>
		public bool Poll()
		{
			if (IsConcluded)
				return false;

			if (mode != null && mode.CheckVictory(teams, out int winnerTeamId))
			{
				IsConcluded = true;
				WinnerTeamId = winnerTeamId;
				return true;
			}

			return false;
		}
	}
}
