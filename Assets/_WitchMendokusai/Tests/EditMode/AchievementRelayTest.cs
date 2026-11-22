using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 업적 주도권 잠금 (사용자 결정 2026-11-22). 게임 업적이 정본, 플랫폼은 연결표에 있는 일부만 받음, 한 방향.
	/// </summary>
	public sealed class AchievementRelayTest
	{
		private const int LINKED_HUNTER = 500;
		private const int LINKED_BOSS = 501;
		private const int GAME_ONLY = 499;

		private sealed class FakePort : IAchievementPlatformPort
		{
			public readonly List<string> Unlocked = new();
			public readonly HashSet<string> Known = new();
			public int FlushCount;

			public bool Unlock(string platformAchievementId)
			{
				Unlocked.Add(platformAchievementId);
				return Known.Contains(platformAchievementId);
			}

			public void Flush() => FlushCount++;
		}

		private static (AchievementRelay Relay, FakePort Port) Create()
		{
			FakePort port = new();
			port.Known.Add("ACH_HUNTER");
			port.Known.Add("ACH_BOSS_HUNTER");
			Dictionary<int, string> map = new()
			{
				{ LINKED_HUNTER, "ACH_HUNTER" },
				{ LINKED_BOSS, "ACH_BOSS_HUNTER" },
			};
			return (new AchievementRelay(map, port), port);
		}

		[Test]
		public void Resync_sends_only_linked_achievements_and_flushes_once()
		{
			(AchievementRelay relay, FakePort port) = Create();

			int relayed = relay.Resync(new[] { GAME_ONLY, LINKED_HUNTER, LINKED_BOSS });

			Assert.That(relayed, Is.EqualTo(2));
			Assert.That(port.Unlocked, Is.EquivalentTo(new[] { "ACH_HUNTER", "ACH_BOSS_HUNTER" }));
			Assert.That(port.FlushCount, Is.EqualTo(1));
		}

		[Test]
		public void Game_only_achievement_never_reaches_platform()
		{
			(AchievementRelay relay, FakePort port) = Create();

			bool relayed = relay.OnAchievementCompleted(GAME_ONLY);

			Assert.That(relayed, Is.False);
			Assert.That(port.Unlocked, Is.Empty);
			Assert.That(port.FlushCount, Is.Zero);
		}

		[Test]
		public void Same_achievement_is_sent_once_per_run()
		{
			(AchievementRelay relay, FakePort port) = Create();

			relay.Resync(new[] { LINKED_HUNTER });
			relay.OnAchievementCompleted(LINKED_HUNTER);

			Assert.That(port.Unlocked, Is.EqualTo(new[] { "ACH_HUNTER" }));
		}

		[Test]
		public void Failed_unlock_is_reported_and_retried_later()
		{
			FakePort port = new();
			AchievementRelay relay = new(new Dictionary<int, string> { { LINKED_HUNTER, "ACH_HUNTER" } }, port);

			// 플랫폼 준비 전: 실패, 기록하지 않음
			UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("ACH_HUNTER"));
			bool first = relay.OnAchievementCompleted(LINKED_HUNTER);
			// 플랫폼 준비 뒤: 다시 시도해 성공
			port.Known.Add("ACH_HUNTER");
			bool second = relay.OnAchievementCompleted(LINKED_HUNTER);
			bool third = relay.OnAchievementCompleted(LINKED_HUNTER);

			Assert.That(first, Is.False);
			Assert.That(second, Is.True);
			Assert.That(third, Is.False, "성공한 것은 다시 적지 않음");
			Assert.That(port.Unlocked.Count, Is.EqualTo(2));
		}
	}
}
