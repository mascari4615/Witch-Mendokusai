using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-2 — <see cref="BuildingProductionModel"/> 생산/소비 가동률식 회귀 잠금.
	///
	/// 순수 함수 — 가동률 = min(노동가용/요구, 재고/요구) clamp[0,1], 소비/산출 = rate×가동률. 가장 부족한
	/// 자원·노동이 병목. 데이터 주도 ResourceId(모드 확장형) + 다입출력. new() 직접 + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class BuildingProductionModelTest
	{
		private static readonly ResourceId RAW = new(0);
		private static readonly ResourceId GOODS = new(1);
		private static readonly ResourceId EXTRA = new(2);

		private static float RateOf(IReadOnlyList<ResourceFlow> flows, ResourceId resource)
		{
			float total = 0f;
			foreach (ResourceFlow flow in flows)
			{
				if (flow.Resource.Equals(resource))
				{
					total += flow.Rate;
				}
			}

			return total;
		}

		// 입력 RAW 2 + 노동 5 → 출력 GOODS 3 레시피.
		private static ProductionRecipe BasicRecipe()
		{
			return new ProductionRecipe(
				new List<ResourceFlow> { new(RAW, 2f) },
				new List<ResourceFlow> { new(GOODS, 3f) },
				laborRequired: 5f);
		}

		[Test]
		public void FullStockAndLabor_FullUtilization()
		{
			BuildingProductionModel model = new();
			Dictionary<ResourceId, float> stock = new() { { RAW, 100f } };

			ProductionResult result = model.Evaluate(BasicRecipe(), stock, availableLabor: 10f);

			Assert.That(result.UtilizationRate, Is.EqualTo(1f).Within(0.0001f), "재고·노동 충분 = 만가동");
			Assert.That(RateOf(result.Consumed, RAW), Is.EqualTo(2f).Within(0.0001f));
			Assert.That(RateOf(result.Produced, GOODS), Is.EqualTo(3f).Within(0.0001f));
		}

		[Test]
		public void InputShortage_ScalesDown()
		{
			BuildingProductionModel model = new();
			Dictionary<ResourceId, float> stock = new() { { RAW, 1f } }; // 요구 2 의 절반

			ProductionResult result = model.Evaluate(BasicRecipe(), stock, availableLabor: 10f);

			Assert.That(result.UtilizationRate, Is.EqualTo(0.5f).Within(0.0001f), "입력 절반 = 가동률 0.5");
			Assert.That(RateOf(result.Consumed, RAW), Is.EqualTo(1f).Within(0.0001f));
			Assert.That(RateOf(result.Produced, GOODS), Is.EqualTo(1.5f).Within(0.0001f));
		}

		[Test]
		public void LaborShortage_ScalesDown()
		{
			BuildingProductionModel model = new();
			Dictionary<ResourceId, float> stock = new() { { RAW, 100f } };

			ProductionResult result = model.Evaluate(BasicRecipe(), stock, availableLabor: 2.5f); // 요구 5 의 절반

			Assert.That(result.UtilizationRate, Is.EqualTo(0.5f).Within(0.0001f), "노동 절반 = 가동률 0.5");
		}

		[Test]
		public void ExtractorNoInputNoLabor_FullUtilization()
		{
			BuildingProductionModel model = new();
			// 채취형 — 입력 0, 노동 0, 출력만. 무에서 생산(부트스트랩).
			ProductionRecipe extractor = new(
				new List<ResourceFlow>(),
				new List<ResourceFlow> { new(RAW, 4f) },
				laborRequired: 0f);

			ProductionResult result = model.Evaluate(extractor, new Dictionary<ResourceId, float>(), availableLabor: 0f);

			Assert.That(result.UtilizationRate, Is.EqualTo(1f).Within(0.0001f), "입력·노동 0 채취형 = 만가동");
			Assert.That(RateOf(result.Produced, RAW), Is.EqualTo(4f).Within(0.0001f));
		}

		[Test]
		public void ZeroStock_ZeroUtilization()
		{
			BuildingProductionModel model = new();

			ProductionResult result = model.Evaluate(BasicRecipe(), new Dictionary<ResourceId, float>(), availableLabor: 10f);

			Assert.That(result.UtilizationRate, Is.EqualTo(0f).Within(0.0001f), "입력 재고 0 = 가동 정지");
			Assert.That(RateOf(result.Produced, GOODS), Is.EqualTo(0f).Within(0.0001f));
		}

		[Test]
		public void MultiInput_BoundByScarcest()
		{
			BuildingProductionModel model = new();
			// 입력 2종 — RAW 충분, EXTRA 부족(요구 4, 재고 2 = 0.5). 가장 부족한 게 가동률.
			ProductionRecipe recipe = new(
				new List<ResourceFlow> { new(RAW, 2f), new(EXTRA, 4f) },
				new List<ResourceFlow> { new(GOODS, 3f) },
				laborRequired: 0f);
			Dictionary<ResourceId, float> stock = new() { { RAW, 100f }, { EXTRA, 2f } };

			ProductionResult result = model.Evaluate(recipe, stock, availableLabor: 100f);

			Assert.That(result.UtilizationRate, Is.EqualTo(0.5f).Within(0.0001f), "최저 입력(EXTRA 0.5)이 병목");
			Assert.That(RateOf(result.Consumed, RAW), Is.EqualTo(1f).Within(0.0001f), "RAW 도 가동률만큼만 소비");
		}
	}
}
