using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 지나가는 것 · 폭주 (TASK-WM-406).
	///
	/// ★ 조사 1순위였다 — 방치형은 기대값이 <b>평탄</b>해서 「지금 이 화면을 볼 이유」가 없다.
	///   여기서 지키는 것은 ① 기다리면 반드시 온다 ② 기다려 주지 않는다
	///   ③ 자리 비운 동안에는 안 걸린다(오프라인으로 새면 「켜 두고 나가기」가 최적이 된다).
	/// </summary>
	public sealed class IdleSurgeTests
	{
		/// <summary>★ 이른 시간에는 안 뜬다 — 켜자마자 뜨면 「기다림」이 없다.</summary>
		[Test]
		public void NothingComes_TooEarly()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			IdleSurge.Advance(state, tuning, tuning.VisitorEarliestSeconds * 0.5d);

			Assert.IsFalse(IdleSurge.CanCatch(state), "너무 일찍 떴다");
		}

		/// <summary>★ 충분히 기다리면 <b>반드시</b> 온다 — 안 오면 기다림이 헛것이 된다.</summary>
		[Test]
		public void ItAlwaysComes_Eventually()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			for (double at = 0d; at < tuning.VisitorLatestSeconds + 5d; at += 1d)
			{
				IdleSurge.Advance(state, tuning, 1d);

				if (IdleSurge.CanCatch(state))
				{
					Assert.Pass();
					return;
				}
			}

			Assert.Fail("가장 늦은 시각을 넘겼는데도 아무것도 안 왔다");
		}

		/// <summary>★ 기다려 주지 않는다 — 안 사라지면 그건 사건이 아니라 버튼이다.</summary>
		[Test]
		public void ItLeavesIfIgnored()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			Summon(state, tuning);

			IdleSurge.Advance(state, tuning, tuning.VisitorStaySeconds + 1d);

			Assert.IsFalse(IdleSurge.CanCatch(state), "가만 뒀는데 안 사라졌다");
		}

		/// <summary>★ 잡으면 판이 실제로 빨라진다 — 그리고 잠시 뒤 원래대로 돌아온다.</summary>
		[Test]
		public void CatchingSpeedsTheFight()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			double calm = IdleModel.AttackSpeedOf(state, tuning);

			Summon(state, tuning);
			Assert.IsTrue(IdleSurge.TryCatch(state, tuning, out IdleSurgeKind caught));
			Assert.AreNotEqual(IdleSurgeKind.None, caught);

			if (caught == IdleSurgeKind.Frenzy)
			{
				Assert.Greater(IdleModel.AttackSpeedOf(state, tuning), calm, "폭주인데 안 빨라졌다");
			}
			else
			{
				// 손 폭주는 <b>때리는 값</b>에 걸린다 — 자동 속도는 그대로다.
				Assert.AreEqual(calm, IdleModel.AttackSpeedOf(state, tuning), 1e-9d);
			}

			IdleSurge.Advance(state, tuning, tuning.SurgeSeconds + 1d);
			Assert.AreEqual(calm, IdleModel.AttackSpeedOf(state, tuning), 1e-9d, "폭주가 안 끝났다");
		}

		/// <summary>
		/// ★ <b>자리 비운 동안에는 안 걸린다</b>.
		///
		/// 새면 「폭주 걸어 놓고 나가기」가 최적 전략이 되어 봉우리의 뜻이 뒤집힌다.
		/// </summary>
		[Test]
		public void SurgeDoesNotLeakIntoOfflineTime()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			Summon(state, tuning);
			IdleSurge.TryCatch(state, tuning, out IdleSurgeKind _);

			state.LastSeenUnixSeconds = 1000L;

			IdleSession session = new IdleSession(tuning, state);
			session.CatchUp(1000L + 3600L);

			Assert.AreEqual(0d, state.SurgeSecondsLeft, 1e-9d, "폭주가 오프라인으로 샜다");
			Assert.AreEqual((int)IdleSurgeKind.None, state.SurgeKind);
		}

		/// <summary>없는 것을 잡을 수는 없다.</summary>
		[Test]
		public void CannotCatchWhatIsNotThere()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			Assert.IsFalse(IdleSurge.TryCatch(state, tuning, out IdleSurgeKind _));
		}

		/// <summary>시험용 — 뜰 때까지 시간을 흘린다.</summary>
		private static void Summon(IdleState state, IdleTuning tuning)
		{
			for (double at = 0d; at < tuning.VisitorLatestSeconds + 5d; at += 1d)
			{
				IdleSurge.Advance(state, tuning, 1d);

				if (IdleSurge.CanCatch(state))
				{
					return;
				}
			}

			Assert.Fail("시험 준비 실패 — 아무것도 안 왔다");
		}
	}
}
