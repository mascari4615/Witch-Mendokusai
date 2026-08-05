using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 전멸 모드 승리 판정 회귀 — 2팀 생존=진행 / 1팀 생존=승 / 상호 전멸=무승부 / 각 팀 1명=진행 / 빈·null 방어.
	/// ScriptableObject.CreateInstance 로 SO 직접 인스턴스화(EditMode, 씬·물리 0). WM-165 item 7.
	/// </summary>
	public class BrawlArenaModeTests
	{
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
		}

		private static MatchTeam Team(int teamId, params bool[] aliveFlags)
		{
			List<ICombatant> members = new();
			for (int i = 0; i < aliveFlags.Length; i++)
			{
				members.Add(new FakeCombatant { CombatantId = (teamId * 10) + i, TeamId = teamId, IsAlive = aliveFlags[i] });
			}
			return new MatchTeam(teamId, members);
		}

		private static BrawlArenaMode Mode()
		{
			return ScriptableObject.CreateInstance<BrawlArenaMode>();
		}

		[Test]
		public void TwoTeamsAlive_MatchOngoing()
		{
			BrawlArenaMode mode = Mode();
			List<MatchTeam> teams = new() { Team(0, true, true, true), Team(1, true, true, true) };

			bool over = mode.CheckVictory(teams, out int winnerTeamId);

			Assert.IsFalse(over, "양 팀 생존 = 진행 중");
			Assert.AreEqual(ArenaModeSO.NO_WINNER, winnerTeamId);
		}

		[Test]
		public void OneTeamWiped_OtherTeamWins()
		{
			BrawlArenaMode mode = Mode();
			List<MatchTeam> teams = new() { Team(0, false, false, false), Team(1, true, false, false) };

			bool over = mode.CheckVictory(teams, out int winnerTeamId);

			Assert.IsTrue(over, "한 팀 전멸 = 매치 종료");
			Assert.AreEqual(1, winnerTeamId, "생존 팀(1) 이 승리");
		}

		[Test]
		public void AllTeamsWiped_Draw()
		{
			BrawlArenaMode mode = Mode();
			List<MatchTeam> teams = new() { Team(0, false, false), Team(1, false, false) };

			bool over = mode.CheckVictory(teams, out int winnerTeamId);

			Assert.IsTrue(over, "상호 전멸 = 매치 종료(무승부)");
			Assert.AreEqual(ArenaModeSO.NO_WINNER, winnerTeamId, "무승부 = 승자 없음");
		}

		[Test]
		public void OneSurvivorPerTeam_StillOngoing()
		{
			BrawlArenaMode mode = Mode();
			List<MatchTeam> teams = new() { Team(0, false, false, true), Team(1, true, false, false) };

			bool over = mode.CheckVictory(teams, out int winnerTeamId);

			Assert.IsFalse(over, "양 팀 각 1명 생존 = 진행 중");
			Assert.AreEqual(ArenaModeSO.NO_WINNER, winnerTeamId);
		}

		[Test]
		public void EmptyOrNullTeams_NotOver()
		{
			BrawlArenaMode mode = Mode();

			Assert.IsFalse(mode.CheckVictory(null, out int winnerNull), "null = 진행(방어)");
			Assert.AreEqual(ArenaModeSO.NO_WINNER, winnerNull);

			Assert.IsFalse(mode.CheckVictory(new List<MatchTeam>(), out int winnerEmpty), "빈 리스트 = 진행(방어)");
			Assert.AreEqual(ArenaModeSO.NO_WINNER, winnerEmpty);
		}
	}
}
