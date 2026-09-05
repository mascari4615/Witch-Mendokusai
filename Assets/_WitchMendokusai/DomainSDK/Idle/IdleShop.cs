namespace WitchMendokusai.DomainSDK.Idle
{
	/// <summary>
	/// 상점에서 골드로 사는 것 (사용자 판정 2026-09-01).
	///
	/// ★ 환생 때 <b>사라짐</b>. 골드로 산 것은 그 판의 것이라는 규칙
	///   (인형 레벨, 강화, 장비와 같은 자리. economy.md 표 3)
	///
	/// ★ 지금은 가방 확장 하나. 상점의 뽑기와는 재화가 다르다 (여기는 골드, 뽑기는 뽑기 재화)
	/// </summary>
	public static class IdleShop
	{
		/// <summary>화면에 적을 한 묶음 칸 수. 튜닝을 못 보는 자리에서 쓴다</summary>
		public const int BAG_STEP_HINT = 10;

		/// <summary>지금 가방이 몇 칸인가. 기본값에 산 만큼 더한 값</summary>
		public static int BagCapacityOf(IdleState state, IdleTuning tuning)
		{
			int bought = state.BagUpgrades;

			if (bought < 0)
			{
				bought = 0;
			}

			if (bought > tuning.BagUpgradeMost)
			{
				bought = tuning.BagUpgradeMost;
			}

			return tuning.BagCapacity + bought * tuning.BagUpgradeStep;
		}

		/// <summary>
		/// 다음 한 칸 묶음에 드는 골드.
		///
		/// ★ 살수록 비싸진다. 안 그러면 골드가 남는 순간 가방이 무한이 되어
		///   합성을 안 하게 된다 (가방이 차는 것이 정리하라는 신호인데 그게 사라짐)
		/// </summary>
		public static double BagUpgradeCost(IdleState state, IdleTuning tuning)
		{
			return tuning.BagUpgradeCostBase
				* System.Math.Pow(tuning.BagUpgradeCostRatio, state.BagUpgrades);
		}

		/// <summary>더 살 수 있나. 상한에 닿았으면 못 산다</summary>
		public static bool CanBuyBag(IdleState state, IdleTuning tuning)
		{
			return state.BagUpgrades < tuning.BagUpgradeMost
				&& state.Resource >= BagUpgradeCost(state, tuning);
		}

		/// <summary>가방을 한 묶음 넓힌다. 골드가 모자라면 아무 일도 안 일어남</summary>
		public static bool TryBuyBag(IdleState state, IdleTuning tuning)
		{
			if (CanBuyBag(state, tuning) == false)
			{
				return false;
			}

			state.Resource -= BagUpgradeCost(state, tuning);
			state.BagUpgrades += 1;
			return true;
		}

		/// <summary>환생이 상점에서 산 것을 지운다 (사용자 판정 2026-09-01)</summary>
		public static void ForgetPurchases(IdleState state)
		{
			state.BagUpgrades = 0;
		}
	}
}
