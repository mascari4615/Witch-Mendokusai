using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-5c — <see cref="CitySimulationSystem"/> 일일 생산/소비 틱 회귀 잠금.
	///
	/// 생산 주문(레시피×건물수)을 CityEconomy 재고에 순차 적용 — 채취/가공/소비, 건물수 스케일, 노동 공유 풀,
	/// 같은 틱 공급망 cascade(앞 주문 산출을 뒤 주문이 소비). new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CitySimulationSystemTest
	{
		private static readonly ResourceId RAW = new(0);
		private static readonly ResourceId GOODS = new(1);

		private static ProductionOrder Extractor(ResourceId output, float rate, int count)
		{
			return new ProductionOrder(
				new ProductionRecipe(new List<ResourceFlow>(), new List<ResourceFlow> { new(output, rate) }, 0f),
				count);
		}

		[Test]
		public void IndustrialExtractor_AddsRawScaledByCount()
		{
			CitySimulationSystem system = new();
			CityEconomy economy = new();

			system.RunDay(economy, new List<ProductionOrder> { Extractor(RAW, 2f, 3) }, availableLabor: 0f);

			Assert.That(economy.GetStock(RAW), Is.EqualTo(6f).Within(0.0001f), "원자재 2×3동 = 6");
		}

		[Test]
		public void Commercial_ConsumesRawProducesGoods()
		{
			CitySimulationSystem system = new();
			CityEconomy economy = new();
			economy.AddStock(RAW, 10f);

			ProductionOrder commercial = new(
				new ProductionRecipe(
					new List<ResourceFlow> { new(RAW, 1f) },
					new List<ResourceFlow> { new(GOODS, 1f) },
					laborRequired: 2f),
				count: 2);

			system.RunDay(economy, new List<ProductionOrder> { commercial }, availableLabor: 100f);

			Assert.That(economy.GetStock(RAW), Is.EqualTo(8f).Within(0.0001f), "원자재 2 소비");
			Assert.That(economy.GetStock(GOODS), Is.EqualTo(2f).Within(0.0001f), "재화 2 생산");
		}

		[Test]
		public void Residential_ConsumesGoodsNoOutput()
		{
			CitySimulationSystem system = new();
			CityEconomy economy = new();
			economy.AddStock(GOODS, 5f);

			ProductionOrder residential = new(
				new ProductionRecipe(
					new List<ResourceFlow> { new(GOODS, 0.5f) },
					new List<ResourceFlow>(),
					laborRequired: 0f),
				count: 4);

			system.RunDay(economy, new List<ProductionOrder> { residential }, availableLabor: 0f);

			Assert.That(economy.GetStock(GOODS), Is.EqualTo(3f).Within(0.0001f), "재화 0.5×4 = 2 소비 → 3 남음");
		}

		[Test]
		public void LaborShortage_ScalesDownProduction()
		{
			CitySimulationSystem system = new();
			CityEconomy economy = new();
			economy.AddStock(RAW, 1000f);

			// 상업 5동, 동당 노동 2 = 10 요구. 가용 5 → 가동률 0.5.
			ProductionOrder commercial = new(
				new ProductionRecipe(
					new List<ResourceFlow> { new(RAW, 1f) },
					new List<ResourceFlow> { new(GOODS, 1f) },
					laborRequired: 2f),
				count: 5);

			system.RunDay(economy, new List<ProductionOrder> { commercial }, availableLabor: 5f);

			Assert.That(economy.GetStock(GOODS), Is.EqualTo(2.5f).Within(0.0001f), "노동 절반 → 재화 5×0.5 = 2.5");
		}

		[Test]
		public void SupplyChainCascade_SameTick()
		{
			CitySimulationSystem system = new();
			CityEconomy economy = new();

			// 빈 재고 시작 — 산업이 원자재 만들고, 같은 틱에 상업이 그걸 소비(주문 순서 = 공급망 순서).
			ProductionOrder industrial = Extractor(RAW, 4f, 1);
			ProductionOrder commercial = new(
				new ProductionRecipe(
					new List<ResourceFlow> { new(RAW, 2f) },
					new List<ResourceFlow> { new(GOODS, 1f) },
					laborRequired: 0f),
				count: 1);

			system.RunDay(economy, new List<ProductionOrder> { industrial, commercial }, availableLabor: 100f);

			Assert.That(economy.GetStock(RAW), Is.EqualTo(2f).Within(0.0001f), "산업 +4, 상업 -2 = 2");
			Assert.That(economy.GetStock(GOODS), Is.EqualTo(1f).Within(0.0001f), "상업 같은 틱에 원자재로 재화 생산");
		}
	}
}
