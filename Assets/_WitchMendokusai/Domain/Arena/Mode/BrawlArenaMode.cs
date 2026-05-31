using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전멸전(Battlerite식 한타) — 마지막까지 한 명이라도 남은 진영이 승리. v1 유일 모드.
	/// 생존 팀 1 = 그 팀 승 / 0 = 무승부(상호 전멸) / 2+ = 진행 중. 팀·멤버 0 = 매치 아님(진행 중 취급).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(BrawlArenaMode), menuName = "WM/Arena/BrawlArenaMode")]
	public class BrawlArenaMode : ArenaModeSO
	{
		public override bool CheckVictory(IReadOnlyList<ArenaTeam> teams, out int winnerTeamId)
		{
			winnerTeamId = NO_WINNER;
			if (teams == null || teams.Count == 0)
				return false;

			int aliveTeamCount = 0;
			int lastAliveTeamId = NO_WINNER;
			foreach (ArenaTeam team in teams)
			{
				if (team != null && team.AnyAlive())
				{
					aliveTeamCount++;
					lastAliveTeamId = team.TeamId;
				}
			}

			// 2팀 이상 생존 = 아직 안 끝남.
			if (aliveTeamCount >= 2)
				return false;

			// 1팀 생존 = 그 팀 승 / 0팀(상호 전멸) = 무승부. 둘 다 매치 종료.
			winnerTeamId = aliveTeamCount == 1 ? lastAliveTeamId : NO_WINNER;
			return true;
		}
	}
}
