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
			Assert.AreEqual(first.State.DamageDealtToTarget, restored.DamageDealtToTarget, TOLERANCE,
				"덜 깎은 피해가 사라졌다 — 자주 저장할수록 손해가 난다");
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
			IdleState state = new IdleState();
			state.LastSeenUnixSeconds = NOON;
			IdleSession session = NewSession(state);

			double credited = session.CatchUp(NOON + 30L * 24L * 3600L);

			Assert.AreEqual(new IdleTuning().MaxOfflineSeconds, credited, TOLERANCE, "상한이 안 걸렸다");
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
	}
}
