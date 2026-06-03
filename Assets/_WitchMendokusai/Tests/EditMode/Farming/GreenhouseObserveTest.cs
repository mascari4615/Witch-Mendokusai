using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-167 Phase 1f 가시화 — 「봐줘야 진짜가 된다」의 관찰 신호/표본 집계 잠금.
	/// GreenhousePlot.Observed / IsSpecimenNow / Greenhouse.SpecimenCount / WitchGreenhouseObject.Observe.
	/// 관찰만이 변수(시듦 통제 = Drain 0 코지)라 "봐준 것만 진짜"가 결정적으로 검증됨.
	/// </summary>
	public sealed class GreenhouseObserveTest
	{
		// 코지(Drain 0=안 시듦) + 빠른 개화(1분=1단계=최종). 시듦 변수 제거.
		private static PlantGrowthParams CozyFast()
		{
			return new PlantGrowthParams(minutesPerStage: 1, maxStage: 1, maxVitality: 100f, drainPerMinute: 0f, tendRestore: 50f);
		}

		// ── GreenhousePlot.Observed ──

		[Test]
		public void Plot_Observed_FalseUntilObserved()
		{
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 1, CozyFast(), startVitality: 100f);

			Assert.That(plot.Observed, Is.False, "심자마자=미관찰");
			plot.Observe();
			Assert.That(plot.Observed, Is.True, "관찰 후=true");
		}

		[Test]
		public void Plot_Observed_FalseWhenEmpty()
		{
			GreenhousePlot plot = new();
			Assert.That(plot.Observed, Is.False, "빈 칸=미관찰(NRE 없음)");
		}

		// ── GreenhousePlot.IsSpecimenNow (관찰 + 개화 + 안시듦) ──

		[Test]
		public void Plot_IsSpecimenNow_RequiresObservedAndBloomed()
		{
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 2, CozyFast(), startVitality: 100f);

			Assert.That(plot.IsSpecimenNow, Is.False, "자라는 중+미관찰=표본 아님");

			plot.Observe();
			Assert.That(plot.IsSpecimenNow, Is.False, "관찰했지만 아직 개화 전=표본 아님");

			plot.Step(1); // 개화
			Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Bloomed));
			Assert.That(plot.IsSpecimenNow, Is.True, "관찰+개화+안시듦=표본");
		}

		[Test]
		public void Plot_IsSpecimenNow_FalseWhenBloomedButUnobserved()
		{
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 3, CozyFast(), startVitality: 100f);
			plot.Step(1); // 관찰 없이 개화

			Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Bloomed));
			Assert.That(plot.IsSpecimenNow, Is.False, "개화했어도 안 봐주면 진짜 아님");
		}

		// ── Greenhouse.SpecimenCount ──

		[Test]
		public void Greenhouse_SpecimenCount_OnlyObservedBloomed()
		{
			Greenhouse greenhouse = new();
			for (int plotId = 0; plotId < 4; plotId++)
			{
				greenhouse.AddPlot(plotId).Plant(plotId, CozyFast(), startVitality: 100f);
			}

			// 칸 1·3만 관찰.
			greenhouse.GetPlot(1).Observe();
			greenhouse.GetPlot(3).Observe();

			Assert.That(greenhouse.SpecimenCount(), Is.Zero, "개화 전엔 표본 0");

			greenhouse.TickWithCarers(null, 1); // 전부 개화(코지)

			Assert.That(greenhouse.SpecimenCount(), Is.EqualTo(2), "봐준 1·3만 진짜=2");
		}

		// ── WitchGreenhouseObject.Observe / SpecimenCount ──

		private static WitchGreenhouseObject MakeHouse()
		{
			GameObject go = new("TestObserveHouse");
			WitchGreenhouseObject house = go.AddComponent<WitchGreenhouseObject>();
			house.CoerceDefaults(); // Start 안 도는 EditMode — minutesPerDay 등 보장(30)
			return house;
		}

		private static WitchPlantSO CozyFastSO()
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults();
			UnityEditor.SerializedObject serialized = new(plant);
			serialized.FindProperty("<DrainPerMinute>k__BackingField").floatValue = 0f;
			serialized.FindProperty("<MinutesPerStage>k__BackingField").intValue = 1;
			serialized.FindProperty("<MaxStage>k__BackingField").intValue = 1;
			serialized.ApplyModifiedProperties();
			return plant;
		}

		[Test]
		public void House_Observe_MarksPlotAndCountsSpecimen()
		{
			WitchGreenhouseObject house = MakeHouse();
			WitchPlantSO plant = CozyFastSO();
			house.Initialize(() => System.Array.Empty<int>());
			house.Plant(0, plant);
			house.Plant(1, plant);

			bool observed = house.Observe(0); // 칸 0만 봐줌

			Assert.That(observed, Is.True);
			Assert.That(house.Model.GetPlot(0).Observed, Is.True);
			Assert.That(house.Model.GetPlot(1).Observed, Is.False);

			house.TickDay(); // 둘 다 개화(코지·빠름)

			Assert.That(house.SpecimenCount, Is.EqualTo(1), "봐준 칸 0만 진짜");

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void House_Observe_MissingPlot_ReturnsFalse()
		{
			WitchGreenhouseObject house = MakeHouse();

			Assert.That(house.Observe(99), Is.False, "없는 칸 관찰=무효");

			Object.DestroyImmediate(house.gameObject);
		}

		// ── Harvest → 영구 표본 (「봐준 건 영영 남는다」) ──

		[Test]
		public void House_Harvest_ObservedBloom_FiresBecameSpecimen()
		{
			WitchGreenhouseObject house = MakeHouse();
			WitchPlantSO plant = CozyFastSO();
			house.Initialize(() => System.Array.Empty<int>());
			house.Plant(0, plant);
			house.Observe(0);   // 봐줌
			house.TickDay();    // 개화

			int specimenPlotId = -1;
			int specimenPlantId = -1;
			house.OnPlotBecameSpecimen += (plotId, plantId) => { specimenPlotId = plotId; specimenPlantId = plantId; };

			bool harvested = house.Harvest(0);

			Assert.That(harvested, Is.True, "개화칸 수확 성공");
			Assert.That(specimenPlotId, Is.EqualTo(0), "관찰된 개체=표본 이벤트 발행");
			Assert.That(specimenPlantId, Is.EqualTo(plant.ID), "표본 식물 id 흐름");
			Assert.That(house.Model.GetPlot(0).IsPlanted, Is.False, "수확 후 칸 비움");

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void House_Harvest_UnobservedBloom_NoSpecimenEvent()
		{
			WitchGreenhouseObject house = MakeHouse();
			WitchPlantSO plant = CozyFastSO();
			house.Initialize(() => System.Array.Empty<int>());
			house.Plant(0, plant);
			house.TickDay();    // 관찰 없이 개화

			int specimenCount = 0;
			house.OnPlotBecameSpecimen += (_, __) => specimenCount++;

			bool harvested = house.Harvest(0);

			Assert.That(harvested, Is.True, "수확 자체는 성공");
			Assert.That(specimenCount, Is.Zero, "안 봐준 개체=표본 이벤트 미발행(진짜 안 됨)");

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void House_Harvest_NotBloomed_ReturnsFalse()
		{
			WitchGreenhouseObject house = MakeHouse();
			WitchPlantSO plant = CozyFastSO();
			house.Initialize(() => System.Array.Empty<int>());
			house.Plant(0, plant);   // 심자마자 = Growing

			Assert.That(house.Harvest(0), Is.False, "개화 전 수확=거부");
			Assert.That(house.Harvest(99), Is.False, "없는 칸 수확=거부");

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}
	}
}
