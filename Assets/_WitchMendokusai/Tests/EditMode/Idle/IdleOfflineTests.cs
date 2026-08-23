using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 저장하고 껐다 켜도 이어지는가, 자리를 비운 동안이 제대로 쳐지는가 (TASK-WM-406).
	///
	/// ★ 방치형에서 가장 조용하게 새는 곳이 여기다 —
	///   덜 깎은 피해를 안 담으면 <b>자주 저장할수록 손해</b>가 나고,
	///   오프라인을 별도 수식으로 만들면 <b>자는 동안만 다른 게임</b>이 된다.
	///   둘 다 사람이 눈치채기 어렵고 눈치챈 뒤엔 이미 저장 형식이 굳어 있다.
	/// </summary>
	public sealed class IdleOfflineTests
	{
		private const double TOLERANCE = 1e-6d;
		private const long NOON = 1_700_000_000L;

		private static IdleSession NewSession(IdleState state = null)
		{
			return new IdleSession(new IdleTuning(), state);
		}

		/// <summary>저장하고 되살리면 그 자리에서 이어진다 — 덜 깎은 피해까지.</summary>
		[Test]
		public void SaveLoad_RestoresEverythingIncludingPartialDamage()
		{
			IdleSession first = NewSession();
			first.Advance(37.5d);
			first.Send(new IdleRaiseUpgradeIntent(IdleUpgradeKind.Damage));
			first.MarkSeen(NOON);

			IdleSaveData saved = first.State.Save();

			IdleState restored = new IdleState();
			restored.Load(saved);

			Assert.AreEqual(first.State.Resource, restored.Resource, TOLERANCE);
			Assert.AreEqual(first.State.Kills, restored.Kills);
			Assert.AreEqual(first.State.HitsOnTarget, restored.HitsOnTarget,
				"때린 횟수가 사라졌다 — 자주 저장할수록 손해가 난다");
			Assert.AreEqual(first.State.AttackProgress, restored.AttackProgress, TOLERANCE,
				"덜 채운 공격이 사라졌다");
			Assert.AreEqual(first.State.Damage.Level, restored.Damage.Level);
			Assert.AreEqual(first.State.AttackSpeed.Level, restored.AttackSpeed.Level);
			Assert.AreEqual(NOON, restored.LastSeenUnixSeconds);
		}

		/// <summary>
		/// ★ 핵심 — 자리를 비운 2시간이 <b>켜 둔 2시간과 똑같다.</b>
		/// 오프라인용 수식을 따로 만들지 않았다는 증거다(코어의 스텝 불변 위에 그냥 얹었다).
		/// </summary>
		[Test]
		public void Offline_EqualsSameTimeOnline()
		{
			IdleSession online = NewSession();
			online.Advance(2d * 3600d);

			IdleState state = new IdleState();
			state.LastSeenUnixSeconds = NOON;
			IdleSession offline = NewSession(state);
			double credited = offline.CatchUp(NOON + 2L * 3600L);

			Assert.AreEqual(2d * 3600d, credited, TOLERANCE);
			Assert.AreEqual(online.State.Kills, offline.State.Kills, "자는 동안 덜 잡았다");
			Assert.AreEqual(online.State.Resource, offline.State.Resource, TOLERANCE, "자는 동안 덜 벌었다");
		}

		/// <summary>상한을 넘겨 비우면 상한까지만 — 한 달 만에 와도 8시간치다.</summary>
		[Test]
		public void Offline_IsCappedByTuning()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.LastSeenUnixSeconds = NOON;
			IdleSession session = NewSession(state);

			double credited = session.CatchUp(NOON + 30L * 24L * 3600L);

			Assert.AreEqual(IdleModel.MaxOfflineFor(state, tuning), credited, TOLERANCE, "상한이 안 걸렸다");
		}

		/// <summary>
		/// ★ 환생하면 <b>덜 매여도 된다</b> — 자리 비워도 되는 시간이 는다.
		///
		/// 근거는 대열 방치 전투 계열다(16시간 → 24시간으로 상한 자체를 늘려 준다).
		/// 방치형에서 이 보상이 특히 제자리다 — 세지는 게 아니라 <b>덜 매이는 것</b>이 상이다.
		/// </summary>
		[Test]
		public void Folding_BuysYouMoreTimeAway()
		{
			IdleTuning tuning = new IdleTuning();

			double fresh = IdleModel.MaxOfflineFor(new IdleState(), tuning);
			double once = IdleModel.MaxOfflineFor(new IdleState { Ascensions = 1 }, tuning);
			double thrice = IdleModel.MaxOfflineFor(new IdleState { Ascensions = 3 }, tuning);

			Assert.AreEqual(8d * 3600d, fresh, TOLERANCE);
			Assert.AreEqual(10d * 3600d, once, TOLERANCE, "한 번 환생했는데 시간이 안 늘었다");
			Assert.Greater(thrice, once);
		}

		/// <summary>
		/// 아무리 환생해도 하루까지 — 끝이 없으면 「하루에 한 번」이 「한 달에 한 번」이 되고,
		/// 그 순간 게임이 아니라 알림이 된다.
		/// </summary>
		[Test]
		public void TimeAway_StopsAtOneDay()
		{
			IdleTuning tuning = new IdleTuning();

			Assert.AreEqual(24d * 3600d, IdleModel.MaxOfflineFor(new IdleState { Ascensions = 999 }, tuning),
				TOLERANCE, "상한의 상한이 없다");
		}

		/// <summary>늘어난 상한이 <b>실제로 쳐진다</b> — 숫자만 늘고 보상이 안 늘면 거짓말이다.</summary>
		[Test]
		public void GrownCap_ActuallyPaysOut()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState fresh = new IdleState { LastSeenUnixSeconds = NOON };
			double freshCredited = new IdleSession(tuning, fresh).CatchUp(NOON + 30L * 3600L);

			IdleState veteran = new IdleState { LastSeenUnixSeconds = NOON, Ascensions = 3 };
			double veteranCredited = new IdleSession(tuning, veteran).CatchUp(NOON + 30L * 3600L);

			Assert.AreEqual(8d * 3600d, freshCredited, TOLERANCE);
			Assert.AreEqual(14d * 3600d, veteranCredited, TOLERANCE, "늘어난 상한이 안 쳐졌다");
		}

		/// <summary>시계를 되감아도 이득이 없다 — 음수는 0으로 본다.</summary>
		[Test]
		public void Offline_ClockRolledBack_GivesNothing()
		{
			IdleState state = new IdleState();
			state.LastSeenUnixSeconds = NOON;
			IdleSession session = NewSession(state);

			double credited = session.CatchUp(NOON - 9999L);

			Assert.AreEqual(0d, credited, TOLERANCE);
			Assert.AreEqual(0L, session.State.Kills, "시계를 되감았는데 뭔가 들어왔다");
		}

		/// <summary>처음 시작은 안 쳐준다 — 1970년부터의 시간을 줄 수는 없다.</summary>
		[Test]
		public void Offline_FirstRun_GivesNothing()
		{
			IdleSession session = NewSession();

			double credited = session.CatchUp(NOON);

			Assert.AreEqual(0d, credited, TOLERANCE);
			Assert.AreEqual(NOON, session.State.LastSeenUnixSeconds, "기준점이 안 찍혔다");
		}

		/// <summary>
		/// 껐다 켜기를 여러 번 해도 총합이 같다 — 한 번에 8시간이든, 1시간씩 여덟 번이든.
		/// 저장을 자주 하는 사람이 손해 보지 않는다는 뜻이다.
		/// </summary>
		[Test]
		public void ManyShortSessions_MatchOneLongSession()
		{
			IdleSession once = NewSession();
			once.Advance(8d * 3600d);

			IdleState state = new IdleState();
			state.LastSeenUnixSeconds = NOON;
			IdleSession many = NewSession(state);

			for (int hour = 1; hour <= 8; hour++)
			{
				IdleSaveData saved = many.State.Save();
				IdleState reloaded = new IdleState();
				reloaded.Load(saved);
				many = NewSession(reloaded);

				many.CatchUp(NOON + hour * 3600L);
			}

			Assert.AreEqual(once.State.Kills, many.State.Kills, "껐다 켤 때마다 조금씩 샜다");
			Assert.AreEqual(once.State.Resource, many.State.Resource, TOLERANCE, "껐다 켤 때마다 조금씩 샜다");
		}

		/// <summary>
		/// ★ 돌아왔을 때 <b>무엇을 벌었는지</b> 알려준다 (TASK-WM-406).
		///
		/// 「N 동안 잡아 뒀다」만으로는 보상이 안 느껴진다 — 방치형의 심장이 여기다.
		/// </summary>
		[Test]
		public void ComingBack_TellsWhatYouEarned()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.LastSeenUnixSeconds = 1000L;

			IdleSession session = new IdleSession(tuning, state);
			session.CatchUp(1000L + 600L, out IdleAwayReport away);

			Assert.IsTrue(away.HasAnything, "쳐준 시간이 0 이다");
			Assert.Greater(away.ResourceGained, 0d, "10분을 비웠는데 번 자원이 0 이다");
			Assert.Greater(away.KillsGained, 0L, "10분을 비웠는데 잡은 게 0 이다");
			Assert.IsFalse(away.HitCap, "10분인데 상한에 걸렸다");
		}

		/// <summary>
		/// ★ 상한에 걸리면 <b>흘린 시간</b>을 말한다.
		///
		/// 안 말하면 사용자는 몇 시간을 흘린 줄도 모르고, 상한을 올릴 이유(환생)도 안 보인다.
		/// 손해는 조용하면 안 된다.
		/// </summary>
		[Test]
		public void HittingTheCap_SaysHowMuchWasLost()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.LastSeenUnixSeconds = 1000L;

			IdleSession session = new IdleSession(tuning, state);
			double cap = IdleModel.MaxOfflineFor(state, tuning);

			session.CatchUp(1000L + (long)cap + 7200L, out IdleAwayReport away);

			Assert.IsTrue(away.HitCap, "상한을 한참 넘겼는데 안 걸렸다고 한다");
			Assert.Greater(away.LostSeconds, 0d, "흘린 시간이 0 이라고 한다");
			Assert.AreEqual(cap, away.CreditedSeconds, 1d, "상한만큼만 쳐줘야 한다");
		}

		/// <summary>
		/// ★ 자리를 안 비웠으면 <b>아무 말도 안 한다</b> — 0 을 보고하는 것도 소음이다.
		/// </summary>
		[Test]
		public void NoTimeAway_SaysNothing()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.LastSeenUnixSeconds = 5000L;

			IdleSession session = new IdleSession(tuning, state);
			session.CatchUp(5000L, out IdleAwayReport away);

			Assert.IsFalse(away.HasAnything, "안 비웠는데 보고할 게 있다고 한다");
		}
	}
}
