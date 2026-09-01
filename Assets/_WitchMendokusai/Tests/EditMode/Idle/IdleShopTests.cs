using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>상점에서 골드로 사는 것 (사용자 판정 2026-09-01, 울티마 스쿼드)</summary>
	public sealed class IdleShopTests
	{
		/// <summary>★ 골드를 내고 가방이 넓어진다</summary>
		[Test]
		public void BuyingBag_WidensIt()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			int before = IdleShop.BagCapacityOf(state, tuning);
			Assert.AreEqual(tuning.BagCapacity, before);

			Assert.IsFalse(IdleShop.TryBuyBag(state, tuning), "골드가 0 인데 샀다");

			state.Resource = tuning.BagUpgradeCostBase;
			Assert.IsTrue(IdleShop.TryBuyBag(state, tuning));

			Assert.AreEqual(before + tuning.BagUpgradeStep, IdleShop.BagCapacityOf(state, tuning));
			Assert.AreEqual(0d, state.Resource, 1e-9d, "값을 안 냈다");
		}

		/// <summary>★ 살수록 비싸진다. 안 그러면 골드가 남는 순간 가방이 무한</summary>
		[Test]
		public void TheCost_Climbs()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			double first = IdleShop.BagUpgradeCost(state, tuning);
			state.BagUpgrades = 4;
			double later = IdleShop.BagUpgradeCost(state, tuning);

			Assert.Greater(later, first * 2d, "네 묶음을 샀는데 값이 두 배도 안 됐다");
		}

		/// <summary>★ 상한이 있다. 무한이면 합성을 안 하게 된다</summary>
		[Test]
		public void ThereIsACeiling()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.BagUpgrades = tuning.BagUpgradeMost;
			state.Resource = 1e12d;

			Assert.IsFalse(IdleShop.CanBuyBag(state, tuning), "상한인데 더 살 수 있다");
			Assert.AreEqual(tuning.BagCapacity + tuning.BagUpgradeMost * tuning.BagUpgradeStep,
				IdleShop.BagCapacityOf(state, tuning));
		}

		/// <summary>★ 넓힌 가방에 실제로 더 들어간다</summary>
		[Test]
		public void TheWiderBag_HoldsMore()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.BagUpgrades = 2;

			for (int at = 0; at < tuning.BagCapacity + tuning.BagUpgradeStep; at++)
			{
				state.Bag.Add(new IdleItem(1, IdleItemSlot.Head));
			}

			Assert.IsFalse(IdleGear.IsBagFull(state, tuning), "넓혔는데 기본 칸에서 막혔다");
		}

		/// <summary>★ 환생이 산 것을 지운다 (사용자 판정 2026-09-01)</summary>
		[Test]
		public void Prestige_ForgetsPurchases()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.BagUpgrades = 5;
			state.Stage = 200;
			state.BestStage = 200;

			IdleModel.TryPrestige(state, tuning, out long _);

			Assert.AreEqual(0, state.BagUpgrades, "환생했는데 가방이 그대로 넓다");
			Assert.AreEqual(tuning.BagCapacity, IdleShop.BagCapacityOf(state, tuning));
		}

		/// <summary>★ 저장을 건넌다</summary>
		[Test]
		public void Purchases_SurviveTheSave()
		{
			IdleState state = new IdleState();
			state.BagUpgrades = 3;

			IdleState back = new IdleState();
			back.Load(state.Save());

			Assert.AreEqual(3, back.BagUpgrades);
		}
	}
}
