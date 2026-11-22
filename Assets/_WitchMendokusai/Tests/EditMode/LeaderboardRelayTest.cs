using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 순위표 주도권 잠금 (사용자 결정 2026-11-22). 기록 정본은 게임 세이브, 플랫폼은 연결표에 있는 기록만 받음, 한 방향.
	/// </summary>
	public sealed class LeaderboardRelayTest
	{
		private const string LINKED = "Dungeon.0.MostKills";
		private const string BOARD = "Dungeon0_MostKills";
		private const string GAME_ONLY = "Dungeon.4.MostKills";

		private SynchronizationContext savedContext;

		private sealed class FakePort : ILeaderboardPlatformPort
		{
			public readonly List<(string Board, int Score)> Calls = new();
			public bool Succeeds = true;
			public TaskCompletionSource<bool> Gate;

			public Task<bool> SubmitBest(string leaderboardName, int score)
			{
				Calls.Add((leaderboardName, score));
				return Gate != null ? Gate.Task : Task.FromResult(Succeeds);
			}
		}

		// 기다림 이어짐이 Unity 문맥으로 미뤄지지 않고 그 자리에서 돌게
		[SetUp]
		public void SetUp()
		{
			savedContext = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext(null);
		}

		[TearDown]
		public void TearDown() => SynchronizationContext.SetSynchronizationContext(savedContext);

		private static (LeaderboardRelay Relay, FakePort Port) Create()
		{
			FakePort port = new();
			Dictionary<string, string> map = new() { { LINKED, BOARD } };
			return (new LeaderboardRelay(map, port), port);
		}

		[Test]
		public void Unlinked_record_stays_in_game()
		{
			(LeaderboardRelay relay, FakePort port) = Create();

			relay.OnBestReported(GAME_ONLY, 30);

			Assert.That(port.Calls, Is.Empty);
		}

		[Test]
		public void Submitted_value_is_not_sent_again()
		{
			(LeaderboardRelay relay, FakePort port) = Create();

			relay.OnBestReported(LINKED, 30);
			relay.Resync(new Dictionary<string, int> { { LINKED, 30 } });
			relay.OnBestReported(LINKED, 45);

			Assert.That(port.Calls, Is.EqualTo(new[] { (BOARD, 30), (BOARD, 45) }));
		}

		[Test]
		public void Failed_submit_is_retried_on_next_report()
		{
			(LeaderboardRelay relay, FakePort port) = Create();
			port.Succeeds = false;
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("순위표 올리기 실패"));

			relay.OnBestReported(LINKED, 30);
			port.Succeeds = true;
			relay.Resync(new Dictionary<string, int> { { LINKED, 30 } });

			Assert.That(port.Calls, Is.EqualTo(new[] { (BOARD, 30), (BOARD, 30) }));
		}

		[Test]
		public void Values_arriving_in_flight_collapse_to_the_last()
		{
			(LeaderboardRelay relay, FakePort port) = Create();
			port.Gate = new TaskCompletionSource<bool>();

			relay.OnBestReported(LINKED, 30);
			relay.OnBestReported(LINKED, 40);
			relay.OnBestReported(LINKED, 50);
			TaskCompletionSource<bool> first = port.Gate;
			port.Gate = null;
			first.SetResult(true);

			Assert.That(port.Calls, Is.EqualTo(new[] { (BOARD, 30), (BOARD, 50) }));
		}

		[Test]
		public void Different_records_upload_one_at_a_time()
		{
			FakePort port = new() { Gate = new TaskCompletionSource<bool>() };
			Dictionary<string, string> map = new() { { LINKED, BOARD }, { "Dungeon.0.HighestLevel", "Dungeon0_HighestLevel" } };
			LeaderboardRelay relay = new(map, port);

			relay.OnBestReported(LINKED, 30);
			relay.OnBestReported("Dungeon.0.HighestLevel", 12);

			Assert.That(port.Calls.Count, Is.EqualTo(1), "Steam 은 진행 중 올리기 1건만 받음");
			TaskCompletionSource<bool> first = port.Gate;
			port.Gate = null;
			first.SetResult(true);
			Assert.That(port.Calls, Is.EqualTo(new[] { (BOARD, 30), ("Dungeon0_HighestLevel", 12) }));
		}

		[Test]
		public void Port_exception_counts_as_failure()
		{
			FakeThrowingPort port = new();
			LeaderboardRelay relay = new(new Dictionary<string, string> { { LINKED, BOARD } }, port);
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("순위표 호출 예외"));
			LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("순위표 올리기 실패"));

			relay.OnBestReported(LINKED, 30);
			relay.OnBestReported(LINKED, 30);

			Assert.That(port.CallCount, Is.EqualTo(2), "예외로 실패한 값은 기록되지 않아 같은 값 알림에서 다시 시도");
		}

		private sealed class FakeThrowingPort : ILeaderboardPlatformPort
		{
			public int CallCount;

			public Task<bool> SubmitBest(string leaderboardName, int score)
			{
				CallCount++;
				if (CallCount == 1)
				{
					throw new System.InvalidOperationException("Steam 준비 전");
				}
				return Task.FromResult(true);
			}
		}
	}
}
