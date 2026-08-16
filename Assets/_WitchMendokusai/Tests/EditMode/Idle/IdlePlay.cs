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
		/// <summary>살 수 있는 것을 다 산다 — 기지 먼저, 그다음 용병.</summary>
		public static void BuyEverything(IdleState state, IdleTuning tuning)
		{
			BuyProducers(state, tuning);
			BuyUpgrades(state, tuning);
		}

		/// <summary>
		/// 기지 — <b>싼 것부터</b> 산다. 쿠키 클리커에서 사람이 실제로 하는 짓이고,
		/// 「비싼 것 하나 vs 싼 것 여럿」의 기본 답이기도 하다.
		/// </summary>
		public static void BuyProducers(IdleState state, IdleTuning tuning)
		{
			state.EnsureProducerRoom(tuning.ProducerCount);

			while (true)
			{
				int cheapest = -1;
				double best = double.PositiveInfinity;

				for (int kind = 0; kind < tuning.ProducerCount; kind++)
				{
					double cost = IdleBase.CostOf(kind, state.Owned[kind], tuning);
					if (cost <= state.Resource && cost < best)
					{
						best = cost;
						cheapest = kind;
					}
				}

				if (cheapest < 0)
				{
					return;
				}

				if (IdleBase.TryBuy(state, tuning, cheapest) == false)
				{
					return;
				}
			}
		}

		/// <summary>용병 — 살 수 있으면 싼 축부터.</summary>
		public static void BuyUpgrades(IdleState state, IdleTuning tuning)
		{
			while (true)
			{
				bool hasDamage = IdleModel.TryGetNextCost(state, tuning, IdleUpgradeKind.Damage, out double damageCost);
				bool hasSpeed = IdleModel.TryGetNextCost(state, tuning, IdleUpgradeKind.AttackSpeed, out double speedCost);

				bool canDamage = hasDamage && damageCost <= state.Resource;
				bool canSpeed = hasSpeed && speedCost <= state.Resource;

				if (canDamage == false && canSpeed == false)
				{
					return;
				}

				IdleUpgradeKind pick = canDamage && (canSpeed == false || damageCost <= speedCost)
					? IdleUpgradeKind.Damage
					: IdleUpgradeKind.AttackSpeed;

				if (IdleModel.TryRaise(state, tuning, pick, out _) == false)
				{
					return;
				}
			}
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
