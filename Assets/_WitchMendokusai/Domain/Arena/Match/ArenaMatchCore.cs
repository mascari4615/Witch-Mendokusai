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
		// 모드 규칙(BrawlArenaMode = 전멸)으로 결착 = true / 시간초과 most-alive = false(ConcludedByTimeout).
		// ⚠ v1 BrawlArenaMode 전제 — 미래 Objective/Lane 모드 추가 시 "전멸" 라벨 매핑 분기 필요.
		public bool ConcludedByElimination { get; private set; }

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

			// mode 는 생성자 필수 의존이다. null 이면 여기서 바로 터져야 한다 —
			// 가드로 넘기면 「규칙 결착이 영영 안 일어나고 매번 타임아웃으로 끝나는」 매치가 되는데,
			// 그건 설정 누락이 아니라 밸런스 문제처럼 보인다. 조용히 틀리는 것보다 시끄럽게 죽는 게 낫다.
			if (mode.CheckVictory(teams, out int winnerTeamId))
			{
				IsConcluded = true;
				WinnerTeamId = winnerTeamId;
				ConcludedByElimination = true; // 규칙 결착 = 전멸(생존팀 ≤1). 상호전멸(winner=NO_WINNER)도 elimination 으로 정확 분류.
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
			// teams 도 생성자 필수 의존 — mode 와 같은 이유로 가드 안 둔다.
			// (팀 *원소* 가 null 인 건 다른 얘기라 아래 loop 의 continue 는 남긴다.)
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
