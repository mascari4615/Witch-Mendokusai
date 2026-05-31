using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-167 마도 온실 Phase 1d — <see cref="WitchGreenhouseObject"/> MonoBehaviour 글루 검증.
	/// D 세션 패턴([[wm-monobehaviour-editmode-decouple]]): RequireComponent·무거운 의존 0 → new GameObject
	/// + AddComponent + Initialize + TickDay 로 틱→자동돌봄→개화/시듦 이벤트를 결정적으로 잠금. PlayMode 불요.
	/// </summary>
	public sealed class WitchGreenhouseObjectTest
	{
		private static WitchGreenhouseObject MakeGreenhouse(int minutesPerDay)
		{
			GameObject go = new("TestGreenhouse");
			WitchGreenhouseObject house = go.AddComponent<WitchGreenhouseObject>();
			// minutesPerDay 는 SerializeField — 테스트용으로 SerializedObject 세팅.
			UnityEditor.SerializedObject serialized = new(house);
			serialized.FindProperty("minutesPerDay").intValue = minutesPerDay;
			serialized.ApplyModifiedProperties();
			return house;
		}

		private static WitchPlantSO WitchPlant(float maxVitality, float drainPerMinute, float tendRestore, int minutesPerStage = 60, int maxStage = 3)
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			UnityEditor.SerializedObject serialized = new(plant);
			serialized.FindProperty("<MinutesPerStage>k__BackingField").intValue = minutesPerStage;
			serialized.FindProperty("<MaxStage>k__BackingField").intValue = maxStage;
			serialized.FindProperty("<MaxVitality>k__BackingField").floatValue = maxVitality;
			serialized.FindProperty("<DrainPerMinute>k__BackingField").floatValue = drainPerMinute;
			serialized.FindProperty("<TendRestore>k__BackingField").floatValue = tendRestore;
			serialized.FindProperty("<StartVitality>k__BackingField").floatValue = maxVitality;
			serialized.ApplyModifiedProperties();
			return plant;
		}

		[Test]
		public void Plant_ThenPlotGrowing()
		{
			WitchGreenhouseObject house = MakeGreenhouse(minutesPerDay: 60);
			WitchPlantSO plant = WitchPlant(maxVitality: 100f, drainPerMinute: 1f, tendRestore: 50f);

			bool planted = house.Plant(plotId: 0, plant);

			Assert.That(planted, Is.True);
			Assert.That(house.Model.GetPlot(0).Phase, Is.EqualTo(PlotPhase.Growing));

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void TickDay_WithCarer_KeepsAliveAndBlooms()
		{
			// 하루 60분, 3단계×60 = 180분 = 3일. 인형 1이 매일 돌봐 생존.
			WitchGreenhouseObject house = MakeGreenhouse(minutesPerDay: 60);
			WitchPlantSO plant = WitchPlant(maxVitality: 100f, drainPerMinute: 1f, tendRestore: 60f);
			house.Initialize(() => new List<int> { 1 });
			house.Plant(0, plant);

			bool bloomedFired = false;
			house.OnPlotBloomed += plotId => bloomedFired = true;

			house.TickDay(); // day1: 돌봄+60, -60 → 생기 100, 60분 생장
			house.TickDay(); // day2: 120분
			house.TickDay(); // day3: 180분 → 개화

			Assert.That(house.Model.GetPlot(0).Phase, Is.EqualTo(PlotPhase.Bloomed));
			Assert.That(bloomedFired, Is.True, "개화 이벤트 발행");

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void TickDay_NoCarer_WithersAndFiresEvent()
		{
			// 하루 60분, 생기 50, drain 1 → 하루 -60 → 첫날 시듦.
			WitchGreenhouseObject house = MakeGreenhouse(minutesPerDay: 60);
			WitchPlantSO plant = WitchPlant(maxVitality: 50f, drainPerMinute: 1f, tendRestore: 0f);
			house.Initialize(() => null); // carer 0
			house.Plant(0, plant);

			bool witheredFired = false;
			int witheredPlot = -1;
			house.OnPlotWithered += plotId => { witheredFired = true; witheredPlot = plotId; };

			house.TickDay();

			Assert.That(house.Model.GetPlot(0).Phase, Is.EqualTo(PlotPhase.Withered));
			Assert.That(witheredFired, Is.True, "시듦 이벤트 발행");
			Assert.That(witheredPlot, Is.EqualTo(0));

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void CozyPlant_ZeroDrain_NeverWithers_EvenNoCarer()
		{
			// Drain 0 (코지/일반작물) → carer 없어도 안 시듦, 시간 지나면 개화.
			WitchGreenhouseObject house = MakeGreenhouse(minutesPerDay: 200);
			WitchPlantSO plant = WitchPlant(maxVitality: 100f, drainPerMinute: 0f, tendRestore: 0f);
			house.Initialize(() => null);
			house.Plant(0, plant);

			house.TickDay(); // 200분 → 3단계(180) 넘김

			Assert.That(house.Model.GetPlot(0).Phase, Is.EqualTo(PlotPhase.Bloomed), "코지 작물=carer 없어도 개화");

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void TickDay_NoEventWhenNoTransition()
		{
			// 개화/시듦 전이 없는 평범한 성장 틱 = 이벤트 0 (전이 감지 정확성).
			WitchGreenhouseObject house = MakeGreenhouse(minutesPerDay: 30);
			WitchPlantSO plant = WitchPlant(maxVitality: 100f, drainPerMinute: 1f, tendRestore: 50f, minutesPerStage: 60, maxStage: 3);
			house.Initialize(() => new List<int> { 1 });
			house.Plant(0, plant);

			int bloomCount = 0;
			int witherCount = 0;
			house.OnPlotBloomed += _ => bloomCount++;
			house.OnPlotWithered += _ => witherCount++;

			house.TickDay(); // 30분 — 아직 1단계 미만, 전이 없음

			Assert.That(house.Model.GetPlot(0).Phase, Is.EqualTo(PlotPhase.Growing));
			Assert.That(bloomCount, Is.Zero);
			Assert.That(witherCount, Is.Zero);

			Object.DestroyImmediate(house.gameObject);
			Object.DestroyImmediate(plant);
		}

		[Test]
		public void Plant_NullSO_Rejected()
		{
			WitchGreenhouseObject house = MakeGreenhouse(minutesPerDay: 60);

			bool planted = house.Plant(0, null);

			Assert.That(planted, Is.False);

			Object.DestroyImmediate(house.gameObject);
		}
	}
}
