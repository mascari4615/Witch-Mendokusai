using System.Reflection;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 저장이 <b>빠뜨린 것 없이</b> 왕복하나 (TASK-WM-406).
	///
	/// ★ 왜 이 시험이 필요한가 — 기능을 얹을 때마다 상태가 늘어나는데, <see cref="IdleState.Save"/> 에
	///   한 줄 빼먹어도 <b>아무도 안 죽는다</b>. 게임은 멀쩡히 돌고, 껐다 켠 다음에야
	///   「내 영웅 어디 갔지」가 된다. 그때는 이미 사용자 저장이 상한 뒤다.
	///
	/// ★ 그래서 <b>기계가 센다</b>: 저장 꼴에 있는 칸이 실제로 왕복하는지 하나씩.
	///   새 칸을 더하면 이 시험이 저절로 그 칸도 본다 — 사람이 목록을 갱신할 필요가 없다.
	///
	/// ★ <b>일부러 안 담는 것</b>은 여기 적어 둔다. 적어 두지 않으면 다음 사람이
	///   「빠뜨린 것」인지 「일부러 뺀 것」인지 알 수 없다.
	/// </summary>
	public sealed class IdleSaveRoundTripTests
	{
		/// <summary>
		/// ★ 저장 꼴의 <b>모든 칸</b>이 실제로 적히고 다시 읽힌다.
		///
		/// ⚠ 처음엔 <b>저장→불러오기→저장</b>을 견줬는데 그건 눈뜬장님이었다 (실측 2026-08-17):
		///   어떤 칸을 아예 안 적으면 양쪽 다 기본값이라 <b>똑같아서 통과</b>한다.
		///   그래서 두 가지를 따로 본다 —
		///   ① 값을 채운 판의 저장은 <b>빈 판의 저장과 달라야</b> 한다(= 적히긴 하나)
		///   ② 불러온 뒤 다시 적은 것이 처음과 같아야 한다(= 읽히긴 하나)
		///   ①이 없으면 「안 적는 칸」이 영영 안 잡힌다.
		/// </summary>
		[Test]
		public void EverySavedFieldSurvives()
		{
			IdleSaveData empty = new IdleState().Save();
			IdleState state = Filled();
			IdleSaveData wrote = state.Save();

			IdleState restored = new IdleState();
			restored.Load(wrote);
			IdleSaveData again = restored.Save();

			FieldInfo[] fields = typeof(IdleSaveData).GetFields(BindingFlags.Public | BindingFlags.Instance);
			Assert.Greater(fields.Length, 10, "저장 꼴이 비었다 — 시험이 아무것도 안 보고 있다");

			foreach (FieldInfo field in fields)
			{
				object blank = field.GetValue(empty);
				object before = field.GetValue(wrote);
				object after = field.GetValue(again);

				if (before is System.Array first)
				{
					// ⚠ 전에는 <b>길이만</b> 봤다 (실측 2026-08-17). 그러면 안이 통째로 뒤바뀌어도
					//   통과한다 — 가방·착용·파티·영웅·생산자가 <b>전부 배열</b>인데.
					//   「빠뜨린 칸을 잡겠다」고 세운 감시가 정작 제일 큰 칸들을 안 보고 있었다.
					System.Array second = (System.Array)after;
					Assert.IsNotNull(second, field.Name + " 가 왕복하며 사라졌다");
					Assert.AreEqual(first.Length, second.Length, field.Name + " 의 길이가 달라졌다");

					bool anythingInside = false;

					for (int at = 0; at < first.Length; at++)
					{
						object one = first.GetValue(at);
						object other = second.GetValue(at);

						Assert.AreEqual(one, other, field.Name + " 의 " + at + "번째가 왕복하며 달라졌다");

						if (one != null && one.Equals(EmptyLike(one)) == false)
						{
							anythingInside = true;
						}
					}

					// 채운 배열이 <b>전부 기본값</b>이면 그 칸을 안 적고 있다는 뜻이다.
					if (first.Length > 0)
					{
						Assert.IsTrue(anythingInside,
							field.Name + " 이 값을 채웠는데도 전부 비어 있다 — Save() 에서 빠졌다");
					}

					continue;
				}

				// ① 채운 판이 빈 판과 같다 = 그 칸을 안 적고 있다.
				Assert.AreNotEqual(blank, before,
					field.Name + " 이 값을 채웠는데도 빈 판과 같다 — Save() 에서 빠졌다");

				// ② 왕복해도 같아야 한다.
				Assert.AreEqual(before, after,
					field.Name + " 가 왕복하며 달라졌다 — Load() 에서 빠졌다");
			}
		}

		/// <summary>
		/// ★ <b>일부러 안 담는 것</b> — 지나가는 것과 폭주.
		///
		/// 담으면 「폭주 걸어 놓고 끄기」가 최적이 되고, 그 순간 봉우리의 뜻이 뒤집힌다.
		/// (자리 비운 몫은 <see cref="IdleSession.CatchUp"/> 이 따로 지운다.)
		/// </summary>
		[Test]
		public void LiveOnlyThingsAreNotSaved()
		{
			IdleState state = Filled();
			state.SurgeKind = (int)IdleSurgeKind.Frenzy;
			state.SurgeSecondsLeft = 25d;
			state.VisitorSecondsLeft = 9d;

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(0d, restored.SurgeSecondsLeft, 1e-9d, "폭주가 저장을 건넜다");
			Assert.AreEqual(0d, restored.VisitorSecondsLeft, 1e-9d, "지나가는 것이 저장을 건넜다");
		}

		/// <summary>★ 빈 저장(옛 판)도 터지지 않고 열린다 — 새 칸은 기본값으로 채운다.</summary>
		[Test]
		public void AnEmptySaveOpensCleanly()
		{
			IdleState fromNothing = new IdleState();
			fromNothing.Load(new IdleSaveData());

			Assert.AreEqual(1, fromNothing.Stage, "단계가 0 이 됐다 — 판이 어긋난다");
			Assert.IsNotNull(fromNothing.Heroes);
			Assert.AreEqual(IdleHeroes.PARTY_SLOTS, fromNothing.Party.Length);
			Assert.IsNotNull(fromNothing.Owned);
		}

		/// <summary>그 자리의 「아무것도 없음」 — 값 꼴이면 기본값.</summary>
		private static object EmptyLike(object one)
		{
			System.Type kind = one.GetType();
			return kind.IsValueType ? System.Activator.CreateInstance(kind) : null;
		}

		/// <summary>값이 <b>다 들어찬</b> 판 — 기본값과 겹치면 빠뜨려도 시험이 못 잡는다.</summary>
		private static IdleState Filled()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.EnsureTierRoom(6);

			state.Resource = 12345.5d;
			state.Kills = 777L;
			state.HitsOnTarget = 5L;
			state.AttackProgress = 0.4d;
			state.Stage = 42;
			state.KillsInStage = 3;
			state.BestStage = 91;
			state.HoldingStage = true;
			state.PrestigePoints = 11L;
			state.Stones = 5L;
			state.Ascensions = 4;
			state.Owned[0] = 9L;
			state.Owned[2] = 2L;
			state.DropSequence = 13L;
			// 배열 안까지 채운다 — 「길이만 맞고 속은 빈」 판으로는 새 감시가 아무것도 못 본다.
			state.DroppedByTier[2] = 7L;
			state.DroppedByTier[4] = 1L;
			state.DropProgressByTier[1] = 0.42d;
			state.RandomState = 987654321L;
			state.BestPotentialValue = 0.33d;
			state.BestPotentialGrade = 2;
			state.PullsDone = 17L;
			state.PullsSincePity = 8;
			state.LastSeenUnixSeconds = 1700000000L;
			state.Damage.Level = 6;
			state.AttackSpeed.Level = 4;
			state.Cost = 4.5d;
			state.SupplySecondsLeft = 12d;
			state.EnsureSeatRoom(tuning);
			state.SeatHealth[0] = 33d;
			state.SeatReviveSeconds[1] = 2.5d;
			state.Repeating = true;
			state.ClearedStage = 41;
			state.MeasuredStage = 40;
			state.MeasuredKillsPerSecond = 1.25d;
			state.PrestigeShards = 6L;
			state.EnsureTicketRoom();
			state.Tickets[0] = 2L;
			state.TicketDay = 20123L;
			state.SpeedStep = 2;
			state.BagUpgrades = 3;
			state.AutoCast = true;

			state.Bag.Add(new IdleItem(3, IdleItemSlot.Hands));
			state.Worn[0] = new IdleItem(2, IdleItemSlot.Head);

			state.Heroes.Add(new IdleHeroOwned(4));
			state.Party[0] = 4;

			return state;
		}

		/// <summary>
		/// ★ 저장의 수가 <b>망가져 있어도</b> 판이 산다 — NaN·무한·음수는 0 으로 받는다.
		///
		/// 이게 없으면 가장 고약한 고장이 난다: <b>안 터지는데 판이 죽는다</b>.
		/// 자원이 한 번 NaN 이 되면 모든 견줌이 거짓이라 아무것도 살 수 없고, 화면은
		/// 「-」만 띄우며 멀쩡히 돈다. 사람은 왜인지 영영 모른다.
		/// </summary>
		[Test]
		public void BrokenNumbersInTheSave_ComeBackSane()
		{
			IdleSaveData saved = new IdleState().Save();
			saved.Resource = double.NaN;
			saved.AttackProgress = double.PositiveInfinity;
			saved.BestPotentialValue = -5d;
			saved.Owned = new long[] { 3L, -7L, 1L };
			saved.DropProgressByTier = new double[] { double.NaN, 0.5d };

			IdleState state = new IdleState();
			state.Load(saved);

			Assert.AreEqual(0d, state.Resource, "자원이 NaN 인 채로 들어왔다");
			Assert.AreEqual(0d, state.AttackProgress, "타격 진행이 무한인 채로 들어왔다");
			Assert.AreEqual(0d, state.BestPotentialValue, "잠재가 음수인 채로 들어왔다");
			Assert.AreEqual(0L, state.Owned[1], "생산자를 음수로 가지고 있다 — 수입이 깎인다");
			Assert.AreEqual(3L, state.Owned[0], "멀쩡한 값까지 지웠다");
			Assert.AreEqual(0d, state.DropProgressByTier[0]);
			Assert.AreEqual(0.5d, state.DropProgressByTier[1], 1e-9d, "멀쩡한 값까지 지웠다");

			// 그리고 <b>판이 실제로 돈다</b> — 수가 다시 NaN 으로 번지지 않는다.
			IdleTuning tuning = new IdleTuning();
			state.EnsureProducerRoom(tuning.ProducerCount);
			IdleSession session = new IdleSession(tuning, state);
			session.Advance(5d);

			Assert.IsFalse(double.IsNaN(state.Resource), "굴렸더니 자원이 다시 NaN 이 됐다");
			Assert.IsFalse(double.IsNaN(IdleBase.OutputPerSecond(state, tuning)));
			Assert.Greater(IdleModel.DamageOf(state, tuning), 0d);
		}

		/// <summary>
		/// ★ <b>셀 수 있는 것</b>이 음수로 적혀 있어도 판이 산다 — 레벨·처치·이번 단계 처치.
		///
		/// 음수 레벨은 값을 거꾸로 만들고, 값이 거꾸로면 <b>올릴수록 약해지는</b> 판이 된다.
		/// 이것도 안 터지고 조용히 틀리는 쪽이라 문 앞에서 본다.
		/// </summary>
		[Test]
		public void NegativeCountsInTheSave_ComeBackAtZero()
		{
			IdleSaveData saved = new IdleState().Save();
			saved.DamageLevel = -4;
			saved.AttackSpeedLevel = -1;
			saved.Kills = -100L;
			saved.KillsInStage = -3;

			IdleState state = new IdleState();
			state.Load(saved);

			Assert.AreEqual(0, state.Damage.Level);
			Assert.AreEqual(0, state.AttackSpeed.Level);
			Assert.AreEqual(0L, state.Kills);
			Assert.AreEqual(0, state.KillsInStage);

			// 그리고 <b>올릴수록 세지는지</b>까지 본다 — 거꾸로 가면 여기서 걸린다.
			IdleTuning tuning = new IdleTuning();
			double before = IdleModel.DamageOf(state, tuning);

			state.Resource = 1e9d;
			Assert.IsTrue(IdleModel.TryRaise(state, tuning, IdleUpgradeKind.Damage, out WitchMendokusai.DomainSDK.Upgrade.UpgradeRaiseFailure _));

			Assert.Greater(IdleModel.DamageOf(state, tuning), before, "올렸는데 약해졌다");
		}
	}
}
