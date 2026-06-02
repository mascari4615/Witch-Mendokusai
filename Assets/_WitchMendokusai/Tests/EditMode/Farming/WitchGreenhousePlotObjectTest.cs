using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-167 마도 온실 Phase 1f — <see cref="WitchGreenhousePlotObject"/> 게임-측 상호작용 검증.
	/// 테마 페이오프 「봐줘야 진짜가 된다」: Growing 상태 OnInteract=관찰(witness)→IsSpecimen 자격.
	/// 두 메커니즘 분리 잠금: 인형 Tend=살림(표본 X) / Fourth Observe=진짜화. PlayMode 불요(D 디커플 패턴).
	/// </summary>
	public sealed class WitchGreenhousePlotObjectTest
	{
		// 코지(안 시듦) 빠른 개화 칸 — 1분 스텝이면 개화. 테스트 결정성용.
		private static PlantGrowthParams CozyFast()
		{
			return new PlantGrowthParams(minutesPerStage: 1, maxStage: 1, maxVitality: 100f, drainPerMinute: 0f, tendRestore: 50f);
		}

		// 마도(시듦) 칸 — 한 스텝이면 생기 0.
		private static PlantGrowthParams MagicFragile()
		{
			return new PlantGrowthParams(minutesPerStage: 1, maxStage: 1, maxVitality: 10f, drainPerMinute: 1f, tendRestore: 50f);
		}

		private static WitchGreenhousePlotObject MakePlotObject()
		{
			GameObject go = new("TestPlotObject");
			return go.AddComponent<WitchGreenhousePlotObject>();
		}

		private static void Cleanup(WitchGreenhousePlotObject plotObject)
		{
			Object.DestroyImmediate(plotObject.gameObject);
		}

		[Test]
		public void OnInteract_Empty_PlantsRuntimeDefault()
		{
			// SO 미할당이어도 런타임 기본작물로 심긴다(asset 불요 정책).
			WitchGreenhousePlotObject plotObject = MakePlotObject();

			plotObject.OnInteract();

			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Growing), "빈 칸 상호작용=심기");
			Cleanup(plotObject);
		}

		[Test]
		public void OnInteract_Growing_FiresObserved()
		{
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 7, CozyFast(), startVitality: 100f);
			// 아직 0분 = Growing.
			plotObject.Bind(plot);

			int observedCount = 0;
			plotObject.OnObserved += _ => observedCount++;

			plotObject.OnInteract();

			Assert.That(observedCount, Is.EqualTo(1), "Growing 상호작용=관찰 이벤트 1회");
			Cleanup(plotObject);
		}

		[Test]
		public void ObservedBloom_Harvest_BecomesSpecimen()
		{
			// 전체 생애: 심김→관찰→개화→수확 = 진짜(표본). 관찰됐으므로 IsSpecimen.
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 42, CozyFast(), startVitality: 100f);
			plotObject.Bind(fieldId: 99, plot);

			plotObject.OnInteract();   // Growing → 관찰
			plot.Step(1);              // 1분 → 개화(MinutesPerStage=1,MaxStage=1)
			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Bloomed));

			HarvestResult harvested = default;
			int harvestCount = 0;
			PlantBecameSpecimenEvent specimen = null;
			plotObject.OnHarvested += result => { harvested = result; harvestCount++; };
			plotObject.OnBecameSpecimen += evt => specimen = evt;

			plotObject.OnInteract();   // Bloomed → 수확

			Assert.That(harvestCount, Is.EqualTo(1), "수확 이벤트 1회");
			Assert.That(harvested.IsSpecimen, Is.True, "관찰된 개체=표본");
			Assert.That(specimen, Is.Not.Null, "표본 이벤트 발행");
			Assert.That(specimen.PlantDataId, Is.EqualTo(42));
			Assert.That(specimen.FieldId, Is.EqualTo(99), "칸 식별자 흐름");
			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Empty), "수확 후 빈 칸");
			Cleanup(plotObject);
		}

		[Test]
		public void UnobservedBloom_Harvest_NotSpecimen()
		{
			// 관찰 안 한 개체는 개화·수확돼도 진짜가 안 됨(표본 이벤트 0). 테마 페이오프의 음화.
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 5, CozyFast(), startVitality: 100f);
			plotObject.Bind(plot);

			plot.Step(1);              // 관찰 없이 개화
			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Bloomed));

			HarvestResult harvested = default;
			int specimenCount = 0;
			plotObject.OnHarvested += result => harvested = result;
			plotObject.OnBecameSpecimen += _ => specimenCount++;

			plotObject.OnInteract();   // 수확

			Assert.That(harvested.IsSpecimen, Is.False, "관찰 안 함=표본 아님");
			Assert.That(specimenCount, Is.Zero, "표본 이벤트 미발행");
			Cleanup(plotObject);
		}

		[Test]
		public void Bind_OperatesOnSharedPlot_NotNewOne()
		{
			// 바인드한 칸 = 상위 Greenhouse 소유 칸. OnInteract 이 새 칸을 만들지 않고 그 칸을 가리킨다.
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			GreenhousePlot shared = new();
			shared.Plant(plantDataId: 3, CozyFast(), startVitality: 100f);
			plotObject.Bind(shared);

			plotObject.OnInteract();   // 관찰 — shared 칸에 작용

			Assert.That(plotObject.Plot, Is.SameAs(shared), "바인드 칸 유지(새 칸 생성 X)");
			Cleanup(plotObject);
		}

		[Test]
		public void OnInteract_Withered_ClearsToEmpty()
		{
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 9, MagicFragile(), startVitality: 10f);
			plotObject.Bind(plot);

			plot.Step(20);             // 생기 10 - 20 → 시듦
			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Withered));

			plotObject.OnInteract();   // 시든 칸 치우기

			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Empty), "시든 칸 상호작용=치움");
			Cleanup(plotObject);
		}

		[Test]
		public void DominantCarer_FlowsToSpecimenEvent()
		{
			// "누가 가장 돌봤나"(변이 입력)가 표본 이벤트의 DominantCarerId 로 흐른다.
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			GreenhousePlot plot = new();
			plot.Plant(plantDataId: 11, CozyFast(), startVitality: 100f);
			plotObject.Bind(plot);

			plotObject.Tend(carerId: 5);
			plotObject.Tend(carerId: 5);
			plotObject.Tend(carerId: 2);
			plotObject.OnInteract();   // 관찰
			plot.Step(1);              // 개화

			PlantBecameSpecimenEvent specimen = null;
			plotObject.OnBecameSpecimen += evt => specimen = evt;

			plotObject.OnInteract();   // 수확

			Assert.That(specimen, Is.Not.Null);
			Assert.That(specimen.DominantCarerId, Is.EqualTo(5), "가장 많이 돌본 carer=변이 입력");
			Cleanup(plotObject);
		}

		[Test]
		public void SetPlant_OverridesPlantedCrop()
		{
			// SetPlant 로 지정한 SO 가 심긴다(ID 확인).
			WitchGreenhousePlotObject plotObject = MakePlotObject();
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults();
			plotObject.SetPlant(plant);

			plotObject.OnInteract();   // 빈 칸 → 심기

			Assert.That(plotObject.Phase, Is.EqualTo(PlotPhase.Growing));
			Assert.That(plotObject.Plot.PlantDataId, Is.EqualTo(plant.ID));
			Object.DestroyImmediate(plant);
			Cleanup(plotObject);
		}
	}
}
