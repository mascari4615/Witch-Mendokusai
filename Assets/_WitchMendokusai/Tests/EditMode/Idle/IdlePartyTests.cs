using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 편성 여섯 칸. 메인 셋은 출전하고, 보조 셋은 불참 (사용자 결정 2026-08-30).
	///
	/// ★ 지키는 것: <b>보조가 정말 불참인가</b>, <b>그래도 몫이 있나</b>.
	///   보조가 출전하면 여섯 명 파티, 몫이 없으면 보조 칸은 장식.
	///   그리고 옛 저장(세 칸)이 <b>메인 칸으로 이어지나</b> (사람이 앉혀 둔 순서 보존).
	/// </summary>
	public sealed class IdlePartyTests
	{
		private static IdleState Owning(params int[] ids)
		{
			IdleState state = new IdleState();

			for (int index = 0; index < ids.Length; index++)
			{
				state.Heroes.Add(new IdleHeroOwned(ids[index]));
			}

			return state;
		}

		[Test]
		public void Party_HasThreeSlots_AllMain()
		{
			IdleState state = new IdleState();

			// 보조 칸은 0 (사용자 판정 2026-09-01: 셋만. 보조 능동 스킬이 생기면 그때)
			Assert.AreEqual(3, state.Party.Length);
			Assert.AreEqual(3, IdleHeroes.MAIN_SLOTS);
			Assert.AreEqual(0, IdleHeroes.SUPPORT_SLOTS);

			for (int slot = 0; slot < state.Party.Length; slot++)
			{
				Assert.AreEqual(-1, state.Party[slot], "새 판의 편성 칸이 비어 있지 않다: " + slot);
			}
		}




		/// <summary>
		/// ★ 옛 저장(세 칸)은 <b>메인 칸으로</b> (사람이 앉혀 둔 셋이 그대로 출전).
		/// 더 긴 저장: 넘치는 칸 버림, 예외 없음.
		/// </summary>
		[Test]
		public void OldThreeSlotSave_LandsInMainSlots()
		{
			IdleSaveData old = new IdleSaveData
			{
				Heroes = new[] { new IdleHeroOwned(4), new IdleHeroOwned(5), new IdleHeroOwned(6) },
				Party = new[] { 6, 4, 5 },
			};

			IdleState state = new IdleState();
			state.Load(old);

			Assert.AreEqual(IdleHeroes.PARTY_SLOTS, state.Party.Length);
			Assert.AreEqual(6, state.Party[0]);
			Assert.AreEqual(4, state.Party[1]);
			Assert.AreEqual(5, state.Party[2]);
			Assert.AreEqual(3, IdleSquad.TakenCount(state), "옛 파티 셋이 메인 자리로 안 왔다");

			IdleSaveData tooLong = new IdleSaveData
			{
				Heroes = new[] { new IdleHeroOwned(0) },
				Party = new[] { 0, -1, -1, -1, -1, -1, -1, -1 },
			};

			IdleState fromLong = new IdleState();
			fromLong.Load(tooLong);
			Assert.AreEqual(IdleHeroes.PARTY_SLOTS, fromLong.Party.Length);
			Assert.AreEqual(0, fromLong.Party[0]);
		}

		/// <summary>여섯 칸 편성이 저장을 한 바퀴 돌아도 그대로다. 보조 칸까지.</summary>
		[Test]
		public void SixSlotParty_SurvivesSaveRoundTrip()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Owning(0, 1, 2, 3, 4, 5);

			for (int slot = 0; slot < state.Party.Length; slot++)
			{
				state.Party[slot] = slot;
			}

			IdleSaveData saved = state.Save();
			IdleState restored = new IdleState();
			restored.Load(saved);

			for (int slot = 0; slot < state.Party.Length; slot++)
			{
				Assert.AreEqual(slot, restored.Party[slot], "편성 칸이 저장을 못 건넜다: " + slot);
			}

			Assert.AreEqual(IdleSquad.TakenCount(state), IdleSquad.TakenCount(restored));
			Assert.AreEqual(IdleHeroes.MAIN_SLOTS, IdleSquad.TakenCount(restored),
				"여섯 다 앉혔는데 출전 수가 메인 셋이 아니다");
			Assert.AreEqual(
				IdleHeroes.PartyMultiplierOf(state, tuning, IdleHeroAxis.Damage),
				IdleHeroes.PartyMultiplierOf(restored, tuning, IdleHeroAxis.Damage), 1e-9d);
		}

		/// <summary>
		/// ★ 자리 0(나)을 뺀 자리는 시작 인형이 채움 (C10). 새 판, 옛 저장, 빈 편성 어디서 와도
		///   전장에 하나. 아니면 처치 0, 골드 0, 게임 정지
		/// </summary>
		[Test]
		public void Starter_FillsTheEmptyField()
		{
			IdleTuning tuning = new IdleTuning();

			IdleSession fresh = new IdleSession(tuning);
			Assert.AreEqual(1, fresh.State.Heroes.Count, "새 판인데 시작 인형이 없다");
			Assert.AreEqual(IdleHeroes.STARTER_ID, fresh.State.Party[0], "시작 인형이 첫 메인 칸에 없다");
			Assert.AreEqual(1, IdleSquad.TakenCount(fresh.State));

			// 자리 0 시절 저장: 인형 0, 자리 넷
			IdleSaveData old = new IdleSaveData { SeatHealth = new[] { 5d, 0d, 0d, 0d }, SeatsReady = true };
			IdleState loaded = new IdleState();
			loaded.Load(old);
			Assert.AreEqual(1, loaded.Heroes.Count, "옛 저장에 시작 인형을 안 줬다");
			Assert.AreEqual(IdleSquad.SEAT_COUNT, loaded.SeatHealth.Length, "옛 자리 넷이 셋으로 안 줄었다");
			Assert.AreEqual(0d, loaded.SeatHealth[0], 1e-12d, "옛 자리 0(나)의 체력이 새 자리 0 으로 새어 들어왔다");

			// 사람이 메인 칸을 다 비워도 하나는 착석 (보조 칸은 없어졌다. 2026-09-01)
			IdleState emptied = Owning(0, 1);

			for (int slot = 0; slot < emptied.Party.Length; slot++)
			{
				emptied.Party[slot] = -1;
			}

			Assert.IsTrue(IdleHeroes.EnsureStarter(emptied));
			Assert.AreEqual(0, emptied.Party[0], "빈 전장을 그대로 뒀다");
			Assert.IsFalse(IdleHeroes.EnsureStarter(emptied), "이미 선 판을 또 바꿨다");
		}
	}
}
