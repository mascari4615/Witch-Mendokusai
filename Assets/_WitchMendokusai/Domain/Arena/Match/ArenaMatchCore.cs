using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 매치 진행 브레인 — 팀 상태를 모드 규칙(ArenaModeSO)으로 매 틱 폴링해 승패를 *단 한 번* 확정.
	/// 제한시간(timeLimitSeconds > 0) 초과 시 **최다 생존 팀 승**(동률 무승부)으로 강제 종료 →
	/// 교착(서로 못 죽여 매치가 영영 안 끝나는 상태) 방지. 순수(MonoBehaviour/물리/이벤트버스 0). EditMode 검증.
	/// </summary>
	public class ArenaMatchCore
	{
		private readonly IReadOnlyList<ArenaTeam> teams;
		private readonly ArenaModeSO mode;
		private readonly float timeLimitSeconds; // <= 0 = 무제한.
		private float elapsedSeconds;

		public bool IsConcluded { get; private set; }
		public int WinnerTeamId { get; private set; } = ArenaModeSO.NO_WINNER;
		public bool ConcludedByTimeout { get; private set; }

		public ArenaMatchCore(IReadOnlyList<ArenaTeam> teams, ArenaModeSO mode, float timeLimitSeconds = 0f)
		{
			this.teams = teams;
			this.mode = mode;
			this.timeLimitSeconds = timeLimitSeconds;
		}

		/// <summary> 시간 미진행 폴 — 모드 규칙만 평가(타임아웃 무관). </summary>
		public bool Poll()
		{
			return Poll(0f);
		}

		/// <summary>
		/// 한 틱 평가(deltaSeconds 경과). 모드 승리 우선, 없으면 제한시간 초과 시 최다 생존 팀 승(동률 무승부).
		/// 종료가 *이번 호출에서 처음* 확정되면 true. 진행 중/이미 종료면 false. 멱등.
		/// </summary>
		public bool Poll(float deltaSeconds)
		{
			if (IsConcluded)
				return false;

			elapsedSeconds += deltaSeconds;

			if (mode != null && mode.CheckVictory(teams, out int winnerTeamId))
			{
				IsConcluded = true;
				WinnerTeamId = winnerTeamId;
				return true;
			}

			if (timeLimitSeconds > 0f && elapsedSeconds >= timeLimitSeconds)
			{
				IsConcluded = true;
				ConcludedByTimeout = true;
				WinnerTeamId = ResolveByMostAlive();
				return true;
			}

			return false;
		}

		// 시간 초과 결착 — 생존 멤버 최다 팀. 단일 최다 = 그 팀 / 동률 = 무승부(NO_WINNER).
		private int ResolveByMostAlive()
		{
			if (teams == null)
				return ArenaModeSO.NO_WINNER;

			int bestTeamId = ArenaModeSO.NO_WINNER;
			int bestAlive = -1;
			bool tie = false;

			foreach (ArenaTeam team in teams)
			{
				if (team == null)
					continue;

				int alive = team.AliveCount();
				if (alive > bestAlive)
				{
					bestAlive = alive;
					bestTeamId = team.TeamId;
					tie = false;
				}
				else if (alive == bestAlive)
				{
					tie = true;
				}
			}

			return tie ? ArenaModeSO.NO_WINNER : bestTeamId;
		}
	}
}
