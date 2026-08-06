using NUnit.Framework;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 「낮의 마계, 밤의 공방」 듀얼루프 자원 브리지 회귀 잠금.
	///
	/// 검증 축:
	///  - DayNightCycle 단계 전환 + 일수 카운터 결정성.
	///  - 채집 → 재고 누적 / 제조 가능·불가 판정 / 판매 → 골드.
	///  - 효율 투자 트랜잭션(부족 시 보존) + 효율 step 함수 단조·clamp.
	///  - "1사이클 닫힘": 낮 채집 → 밤 제조·판매 → 효율 투자 → 다음 낮 채집량 증가.
	///
	/// 순수 — MonoBehaviour/VContainer/PlayMode 0. new() 직접.
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class WorkshopLedgerTest
	{
		private static readonly MaterialId MagicHerb = new(1);
		private static readonly MaterialId DemonBone = new(2);

		private static WorkshopProduct MakePotion(int salePrice = 30)
		{
			return new WorkshopProduct(
				productId: 1,
				materials: new[]
				{
					new MaterialCost(MagicHerb, 2),
					new MaterialCost(DemonBone, 1),
				},
				salePrice: salePrice);
		}

		private static DayEfficiencyCoefficients Coeffs()
		{
			return new DayEfficiencyCoefficients(
				baseEfficiency: 1f,
				goldPerEfficiencyStep: 50f,
				efficiencyPerStep: 0.2f,
				maxEfficiency: 3f);
		}

		[Test]
		public void DayNightCycle_Advance_DayToNight_PreservesDayIndex()
		{
			DayNightCycle cycle = new(DayNightPhase.Day, 0);

			cycle.Advance();

			Assert.That(cycle.Phase, Is.EqualTo(DayNightPhase.Night), "낮 → 밤");
			Assert.That(cycle.DayIndex, Is.EqualTo(0), "낮→밤 전환은 일수 증가 X(아직 1일 안 끝남)");
		}

		[Test]
		public void DayNightCycle_Advance_NightToDay_IncrementsDayIndex()
		{
			DayNightCycle cycle = new(DayNightPhase.Night, 0);

			cycle.Advance();

			Assert.That(cycle.Phase, Is.EqualTo(DayNightPhase.Day), "밤 → 다음 날 아침");
			Assert.That(cycle.DayIndex, Is.EqualTo(1), "밤→낮 = 1사이클 닫힘 → 일수 +1");
		}

		[Test]
		public void CollectMaterial_AccumulatesStock()
		{
			WorkshopLedger ledger = new();

			ledger.CollectMaterial(MagicHerb, 5);
			ledger.CollectMaterial(MagicHerb, 3);

			Assert.That(ledger.GetStock(MagicHerb), Is.EqualTo(8));
		}

		[Test]
		public void CollectMaterial_NonPositive_Ignored()
		{
			WorkshopLedger ledger = new();

			ledger.CollectMaterial(MagicHerb, 0);
			ledger.CollectMaterial(MagicHerb, -5);

			Assert.That(ledger.GetStock(MagicHerb), Is.EqualTo(0), "0/음수 채집은 무효");
		}

		[Test]
		public void TryManufacture_StockInsufficient_FalseAndStockPreserved()
		{
			WorkshopLedger ledger = new();
			ledger.CollectMaterial(MagicHerb, 1);

			bool produced = ledger.TryManufacture(MakePotion());

			Assert.That(produced, Is.False, "재료 부족 → 제조 불가");
			Assert.That(ledger.GetStock(MagicHerb), Is.EqualTo(1), "실패는 재고 소비 X (트랜잭션 보존)");
			Assert.That(ledger.GetStock(DemonBone), Is.EqualTo(0));
		}

		[Test]
		public void TryManufacture_StockSufficient_TrueAndMaterialsConsumed()
		{
			WorkshopLedger ledger = new();
			ledger.CollectMaterial(MagicHerb, 5);
			ledger.CollectMaterial(DemonBone, 3);

			bool produced = ledger.TryManufacture(MakePotion());

			Assert.That(produced, Is.True);
			Assert.That(ledger.GetStock(MagicHerb), Is.EqualTo(3), "Herb 2 차감");
			Assert.That(ledger.GetStock(DemonBone), Is.EqualTo(2), "Bone 1 차감");
		}

		[Test]
		public void SellProduct_AccumulatesGold()
		{
			WorkshopLedger ledger = new();

			ledger.SellProduct(MakePotion(salePrice: 30), unitsSold: 4);

			Assert.That(ledger.Gold, Is.EqualTo(120));
		}

		[Test]
		public void SellProduct_NonPositiveUnits_NoOp()
		{
			WorkshopLedger ledger = new();

			ledger.SellProduct(MakePotion(salePrice: 30), unitsSold: 0);
			ledger.SellProduct(MakePotion(salePrice: 30), unitsSold: -2);

			Assert.That(ledger.Gold, Is.EqualTo(0), "0/음수 판매는 골드 변화 X");
		}

		[Test]
		public void InvestInDayEfficiency_GoldSufficient_DeductsAndAccumulatesInvestment()
		{
			WorkshopLedger ledger = new();
			ledger.SellProduct(MakePotion(salePrice: 100), unitsSold: 2); // gold 200

			bool invested = ledger.InvestInDayEfficiency(150);

			Assert.That(invested, Is.True);
			Assert.That(ledger.Gold, Is.EqualTo(50));
			Assert.That(ledger.GoldInvestedInDayEfficiency, Is.EqualTo(150));
		}

		[Test]
		public void InvestInDayEfficiency_GoldInsufficient_FalseAndStatePreserved()
		{
			WorkshopLedger ledger = new();
			ledger.SellProduct(MakePotion(salePrice: 10), unitsSold: 1); // gold 10

			bool invested = ledger.InvestInDayEfficiency(50);

			Assert.That(invested, Is.False, "골드 부족 → 투자 거부");
			Assert.That(ledger.Gold, Is.EqualTo(10), "실패는 골드 보존");
			Assert.That(ledger.GoldInvestedInDayEfficiency, Is.EqualTo(0));
		}

		[Test]
		public void DayEfficiencyModel_ZeroInvestment_ReturnsBaseEfficiency()
		{
			float efficiency = DayEfficiencyModel.Evaluate(0, Coeffs());

			Assert.That(efficiency, Is.EqualTo(1f), "투자 0 = baseEfficiency");
		}

		[Test]
		public void DayEfficiencyModel_OneStep_AddsEfficiencyPerStep()
		{
			float oneStep = DayEfficiencyModel.Evaluate(50, Coeffs());

			Assert.That(oneStep, Is.EqualTo(1.2f).Within(0.001f), "1 step = base + perStep");
		}

		[Test]
		public void DayEfficiencyModel_AboveCeiling_ClampedToMax()
		{
			float efficiency = DayEfficiencyModel.Evaluate(10_000, Coeffs());

			Assert.That(efficiency, Is.EqualTo(3f), "ceiling 너머 invest 해도 maxEfficiency");
		}

		[Test]
		public void DayEfficiencyModel_MonotonicallyNonDecreasing()
		{
			DayEfficiencyCoefficients coeffs = Coeffs();

			// 같은 step 안에선 평탄, step 경계 넘으면 +. 절대 감소 X.
			float prev = DayEfficiencyModel.Evaluate(0, coeffs);
			for (int gold = 1; gold <= 500; gold += 1)
			{
				float current = DayEfficiencyModel.Evaluate(gold, coeffs);
				Assert.That(current, Is.GreaterThanOrEqualTo(prev), $"투자 {gold} 에서 효율 감소 발견");
				prev = current;
			}
		}

		[Test]
		public void DualLoop_SingleCycle_Closes_NextDayCollectsMore()
		{
			// 1사이클 수치 닫힘 — Phase 0 검수 메인.
			// 낮1(eff 1.0) → 채집 → 밤1 → 제조·판매 → 골드 → 효율 투자 → 낮2(eff > 1.0)
			DayEfficiencyCoefficients coeffs = Coeffs();
			WorkshopLedger ledger = new();
			DayNightCycle cycle = new();

			// --- 낮 1 ---
			Assert.That(cycle.Phase, Is.EqualTo(DayNightPhase.Day));
			float dayOneEff = ledger.CurrentDayEfficiency(coeffs);
			Assert.That(dayOneEff, Is.EqualTo(1f), "첫 낮은 baseline");

			int dayOneHerbs = DayEfficiencyModel.ScaleCollection(10, dayOneEff);
			int dayOneBones = DayEfficiencyModel.ScaleCollection(5, dayOneEff);
			ledger.CollectMaterial(MagicHerb, dayOneHerbs);
			ledger.CollectMaterial(DemonBone, dayOneBones);
			cycle.Advance(); // → Night

			// --- 밤 1 ---
			Assert.That(cycle.Phase, Is.EqualTo(DayNightPhase.Night));
			WorkshopProduct potion = MakePotion(salePrice: 50);
			int produced = 0;
			while (ledger.TryManufacture(potion))
			{
				produced = produced + 1;
			}
			ledger.SellProduct(potion, produced);
			int goldAfterNight = ledger.Gold;
			cycle.Advance(); // → Day, DayIndex +1

			Assert.That(produced, Is.GreaterThan(0), "낮 채집분으로 최소 1개 제조 가능");
			Assert.That(goldAfterNight, Is.GreaterThan(0), "판매 → 골드 +");
			Assert.That(cycle.DayIndex, Is.EqualTo(1), "1사이클 닫힘 → DayIndex +1");

			// --- 다음 낮 시작 — 효율 투자로 다음 낮 채집량 증가 검수 ---
			bool invested = ledger.InvestInDayEfficiency(50);
			Assert.That(invested, Is.True);
			float dayTwoEff = ledger.CurrentDayEfficiency(coeffs);

			Assert.That(dayTwoEff, Is.GreaterThan(dayOneEff), "투자 후 다음 낮 효율 상승");

			int dayTwoHerbs = DayEfficiencyModel.ScaleCollection(10, dayTwoEff);
			Assert.That(dayTwoHerbs, Is.GreaterThan(dayOneHerbs), "같은 base 채집량으로도 다음 낮은 더 많이 수확 (브리지 닫힘)");
		}
	}
}
