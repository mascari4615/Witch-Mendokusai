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
			List<ArenaTeam> teams = new()
			{
				new ArenaTeam(0, new List<ICombatant> { a }),
				new ArenaTeam(1, new List<ICombatant> { b }),
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
			List<ArenaTeam> teams = new()
			{
				new ArenaTeam(0, new List<ICombatant> { a }),
				new ArenaTeam(1, new List<ICombatant> { b }),
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
			List<ArenaTeam> teams = new()
			{
				new ArenaTeam(0, new List<ICombatant> { a }),
				new ArenaTeam(1, new List<ICombatant> { b }),
			};
			ArenaMatchCore core = new(teams, Mode());

			Assert.IsTrue(core.Poll(), "상호 전멸 = 종료");
			Assert.IsTrue(core.IsConcluded);
			Assert.AreEqual(ArenaModeSO.NO_WINNER, core.WinnerTeamId, "무승부 = 승자 없음");
		}

		[Test]
		public void Poll_NullMode_NeverConcludes()
		{
			List<ArenaTeam> teams = new() { new ArenaTeam(0, new List<ICombatant>()) };
			ArenaMatchCore core = new(teams, null);

			Assert.IsFalse(core.Poll(), "모드 없음 = 종료 안 함(방어)");
			Assert.IsFalse(core.IsConcluded);
		}
	}
}
