using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 시험에서 쓰는 <b>사람 흉내</b> — 두 층을 다 산다 (TASK-WM-406).
	///
	/// ★ 왜 한자리에 모았나 — 층이 둘이 되면서(기지 · 모험) 「살 수 있으면 산다」가
	///   파일마다 달라지기 시작했다. 정책이 파일마다 다르면 <b>시험끼리 다른 게임을 잰다</b>.
	///
	/// ★ 정책은 <b>사람이 하는 짓에 가깝게</b>: 기지를 먼저 굴려 자원을 만들고,
	///   남는 자원으로 용병을 올린다. 기지가 없으면 자원이 아예 안 생기므로 그게 순서다.
	/// </summary>
	public static class IdlePlay
	{
		/// <summary>살 수 있는 것을 다 산다 — 기지 먼저, 그다음 용병. 막혀 있으면 다시 도전한다.</summary>
		public static void BuyEverything(IdleState state, IdleTuning tuning)
		{
			BuyProducers(state, tuning);
			BuyUpgrades(state, tuning);
			PushOnAfterFailure(state, tuning);
		}

		/// <summary>
		/// 실패해서 <b>반복</b> 중이면 「다음 구역」을 누른다 (V2 방향 6).
		///
		/// ★ V2 부터 전멸은 <b>자동으로 안 풀린다</b> — 다시 갈지는 사람이 정한다.
		///   시뮬은 사람 흉내라 그 손도 대신해야 한다. 안 그러면 시뮬이 첫 벽에서 영영 멎고,
		///   그렇게 뽑은 곡선 표는 <b>아무도 안 노는 판</b>의 것이 된다.
		/// </summary>
		public static void PushOnAfterFailure(IdleState state, IdleTuning tuning)
		{
			if (state.Repeating)
			{
				IdleSquad.TryAdvanceStage(state, tuning);
			}
		}

		/// <summary>
		/// 기지 — <b>싼 것부터</b> 산다. 쿠키 클리커에서 사람이 실제로 하는 짓이고,
		/// 「비싼 것 하나 vs 싼 것 여럿」의 기본 답이기도 하다.
		/// </summary>
		public static void BuyProducers(IdleState state, IdleTuning tuning)
		{
			state.EnsureProducerRoom(tuning.ProducerCount);

			// ⚠ 여기도 <b>규칙이 두 벌</b>이었다 (실측 2026-08-17). 이 고리가 고르는 셈을
			//   따로 쓰고 있었고, 그래서 <b>아직 화면에 안 보이는 생산자까지</b> 샀다
			//   (게임은 앞 단계를 안 사면 다음 줄을 안 보여 준다 — IdleBase.IsHidden).
			//   사람이 못 하는 짓을 시뮬이 하면, 그 시뮬로 뽑은 곡선 표는 <b>아무도 안 노는 판</b>의 것이다.
			//   이제 게임의 몰아 사기와 <b>같은 한 걸음</b>을 부른다.
			IdleBase.BuyAsManyAsAfforded(state, tuning, int.MaxValue);
		}

		/// <summary>
		/// 용병 — 살 수 있으면 싼 축부터.
		///
		/// ★ 규칙은 <b>코어에 있다</b>(<see cref="IdleModel.RaiseAsManyAsAfforded"/>).
		///   전에는 이 규칙이 시험에만 있고 게임에는 없었다 — 그러면 사람은 시험보다 못한 판을 논다.
		///   게임에 올린 뒤로는 여기서 그걸 부른다. 두 벌로 두면 언젠가 갈린다.
		/// </summary>
		public static void BuyUpgrades(IdleState state, IdleTuning tuning)
		{
			IdleModel.RaiseAsManyAsAfforded(state, tuning, int.MaxValue);
		}

		/// <summary>
		/// 기지를 <paramref name="seconds"/> 초만큼 미리 굴려 둔다 —
		/// 「자원이 있는 상태」를 만들려는 시험이 매번 같은 방식을 쓰게.
		/// </summary>
		public static void Prime(IdleState state, IdleTuning tuning, double seconds)
		{
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] += 1L;

			const double TICK = 10d;
			for (double elapsed = 0d; elapsed < seconds; elapsed += TICK)
			{
				IdleModel.Step(state, tuning, TICK);
				BuyEverything(state, tuning);
			}
		}
	}
}
