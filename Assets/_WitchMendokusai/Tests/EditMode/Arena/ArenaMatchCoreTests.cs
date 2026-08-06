using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 매치 코어 회귀 — 진행/종료-once 멱등/승자 보관/무승부/모드 null 방어.
	/// FakeCombatant 의 IsAlive 를 폴 사이에 바꿔 "전투 중 전멸"을 시뮬레이트. 씬·물리 0. WM-165 item 8.
	/// </summary>
	public class ArenaMatchCoreTests
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

		private static BrawlArenaMode Mode()
		{
			return ScriptableObject.CreateInstance<BrawlArenaMode>();
		}

		[Test]
		public void Poll_TwoTeamsAlive_NotConcluded()
		{
			FakeCombatant a = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant b = new() { CombatantId = 1, TeamId = 1 };
			List<MatchTeam> teams = new()
			{
				new MatchTeam(0, new List<ICombatant> { a }),
				new MatchTeam(1, new List<ICombatant> { b }),
			};
			ArenaMatchCore core = new(teams, Mode());

			Assert.IsFalse(core.Poll(), "양 팀 생존 = 미종료");
			Assert.IsFalse(core.IsConcluded);
			Assert.AreEqual(ArenaModeSO.NO_WINNER, core.WinnerTeamId);
		}

		[Test]
		public void Poll_TeamWiped_ConcludesOnceWithWinner()
		{
			FakeCombatant a = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant b = new() { CombatantId = 1, TeamId = 1 };
			List<MatchTeam> teams = new()
			{
				new MatchTeam(0, new List<ICombatant> { a }),
				new MatchTeam(1, new List<ICombatant> { b }),
			};
			ArenaMatchCore core = new(teams, Mode());

			Assert.IsFalse(core.Poll(), "초기 = 진행 중");

			b.IsAlive = false; // 팀1 전멸

			Assert.IsTrue(core.Poll(), "전멸 시점 첫 폴 = 종료 확정(true)");
			Assert.IsTrue(core.IsConcluded);
			Assert.AreEqual(0, core.WinnerTeamId, "팀0 승");

			Assert.IsFalse(core.Poll(), "이미 종료 = 이후 폴 false(멱등)");
			Assert.AreEqual(0, core.WinnerTeamId, "승자 유지");
		}

		[Test]
		public void Poll_MutualWipe_ConcludesAsDraw()
		{
			FakeCombatant a = new() { CombatantId = 0, TeamId = 0, IsAlive = false };
			FakeCombatant b = new() { CombatantId = 1, TeamId = 1, IsAlive = false };
			List<MatchTeam> teams = new()
			{
				new MatchTeam(0, new List<ICombatant> { a }),
				new MatchTeam(1, new List<ICombatant> { b }),
			};
			ArenaMatchCore core = new(teams, Mode());

			Assert.IsTrue(core.Poll(), "상호 전멸 = 종료");
			Assert.IsTrue(core.IsConcluded);
			Assert.AreEqual(ArenaModeSO.NO_WINNER, core.WinnerTeamId, "무승부 = 승자 없음");
		}

		[Test]
		public void Poll_NullMode_NeverConcludes()
		{
			List<MatchTeam> teams = new() { new MatchTeam(0, new List<ICombatant>()) };
			ArenaMatchCore core = new(teams, null);

			Assert.IsFalse(core.Poll(), "모드 없음 = 종료 안 함(방어)");
			Assert.IsFalse(core.IsConcluded);
		}

		[Test]
		public void Timeout_MoreAliveTeamWins()
		{
			FakeCombatant a0 = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant a1 = new() { CombatantId = 1, TeamId = 0 };
			FakeCombatant b0 = new() { CombatantId = 2, TeamId = 1 };
			List<MatchTeam> teams = new()
			{
				new MatchTeam(0, new List<ICombatant> { a0, a1 }), // 2 생존
				new MatchTeam(1, new List<ICombatant> { b0 }),      // 1 생존
			};
			ArenaMatchCore core = new(teams, Mode(), 1.0f);

			Assert.IsFalse(core.Poll(0.5f), "0.5s — 양 팀 생존, 미종료");
			Assert.IsTrue(core.Poll(0.6f), "누적 1.1s ≥ 1.0 — 시간초과 종료");
			Assert.IsTrue(core.ConcludedByTimeout);
			Assert.AreEqual(0, core.WinnerTeamId, "팀0(2 생존) > 팀1(1) → 팀0 승");
		}

		[Test]
		public void Timeout_EqualAlive_Draw()
		{
			FakeCombatant a = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant b = new() { CombatantId = 1, TeamId = 1 };
			List<MatchTeam> teams = new()
			{
				new MatchTeam(0, new List<ICombatant> { a }),
				new MatchTeam(1, new List<ICombatant> { b }),
			};
			ArenaMatchCore core = new(teams, Mode(), 1.0f);

			Assert.IsTrue(core.Poll(1.0f), "시간초과 종료");
			Assert.IsTrue(core.ConcludedByTimeout);
			Assert.AreEqual(ArenaModeSO.NO_WINNER, core.WinnerTeamId, "각 1 생존 동률 → 무승부");
		}


		// --- 3팀 이상: 「앞선 동률이 나중에 깨지는」 분기 ---
		//
		// 시간초과 판정은 최다 생존을 훑으면서 동률 플래그를 세우고, **더 높은 팀이 나중에 나오면
		// 그 플래그를 도로 내려야 한다.** 2팀만으로는 이 분기를 못 밟는다(동률이 서면 그걸로 끝).
		// 내리는 걸 잊으면 명백한 승자가 있는데 무승부가 나오고, 화면에선 「왜 비겼지」로만 보인다.
		// 3팀 = 리그(WM-165 의 목표)에서 바로 나오는 모양이라 가상의 경우가 아니다.

		private static MatchTeam TeamWithAlive(int teamId, int aliveCount)
		{
			List<ICombatant> members = new();
			for (int i = 0; i < aliveCount; i++)
				members.Add(new FakeCombatant { CombatantId = teamId * 100 + i, TeamId = teamId });
			return new MatchTeam(teamId, members);
		}

		[Test]
		public void Timeout_3팀_앞선_동률을_나중_최다가_깬다()
		{
			// 2 / 2 / 3 — 팀0·팀1 이 먼저 동률을 세우고, 팀2 가 그걸 깬다.
			List<MatchTeam> teams = new() { TeamWithAlive(0, 2), TeamWithAlive(1, 2), TeamWithAlive(2, 3) };
			ArenaMatchCore core = new(teams, Mode(), 1.0f);

			Assert.IsTrue(core.Poll(1.0f), "시간초과 종료");
			Assert.IsTrue(core.ConcludedByTimeout);
			Assert.AreEqual(2, core.WinnerTeamId, "팀2(3 생존)가 단독 최다 — 앞선 2:2 동률이 승부를 먹으면 안 된다");
		}

		[Test]
		public void Timeout_3팀_최다가_동률이면_무승부()
		{
			// 3 / 3 / 2 — 최다가 둘. 아래 팀이 뒤에 와도 동률은 유지돼야 한다.
			List<MatchTeam> teams = new() { TeamWithAlive(0, 3), TeamWithAlive(1, 3), TeamWithAlive(2, 2) };
			ArenaMatchCore core = new(teams, Mode(), 1.0f);

			Assert.IsTrue(core.Poll(1.0f), "시간초과 종료");
			Assert.IsTrue(core.ConcludedByTimeout);
			Assert.AreEqual(ArenaModeSO.NO_WINNER, core.WinnerTeamId, "최다가 3으로 둘 — 무승부");
		}

		[Test]
		public void Timeout_전멸한_팀은_최다_계산에서_빠진다()
		{
			// 0 / 1 — 전멸 팀이 첫 번째로 와도 0 이 최다가 되면 안 된다.
			List<MatchTeam> teams = new() { TeamWithAlive(0, 0), TeamWithAlive(1, 1) };
			ArenaMatchCore core = new(teams, Mode(), 1.0f);

			core.Poll(1.0f);
			Assert.AreEqual(1, core.WinnerTeamId, "생존 1 > 생존 0");
		}

		[Test]
		public void EliminationBeforeTimeout_ModeWinsNotTimeout()
		{
			FakeCombatant a = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant b = new() { CombatantId = 1, TeamId = 1 };
			List<MatchTeam> teams = new()
			{
				new MatchTeam(0, new List<ICombatant> { a }),
				new MatchTeam(1, new List<ICombatant> { b }),
			};
			ArenaMatchCore core = new(teams, Mode(), 10f);

			Assert.IsFalse(core.Poll(1f), "진행 중");
			b.IsAlive = false;
			Assert.IsTrue(core.Poll(1f), "전멸 = 모드 승리(타임아웃 전)");
			Assert.IsFalse(core.ConcludedByTimeout, "타임아웃 아님 — 모드 종료");
			Assert.AreEqual(0, core.WinnerTeamId);
		}
	}
}
