using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-167 마도 온실 Phase 1c — <see cref="WitchPlantSO"/> 가 순수 성장모델의 게임 데이터
	/// producer 임을 검증(데드 SO 아님). SO 수치 → PlantGrowthParams → WitchPlantGrowth.Step 통합.
	/// SerializedObject 로 [field: SerializeField] backing field 를 세팅(EditMode 표준).
	/// </summary>
	public sealed class WitchPlantSOTest
	{
		private static WitchPlantSO MakeSO(int minutesPerStage, int maxStage, float maxVitality, float drainPerMinute, float tendRestore)
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			SerializedObject serialized = new(plant);
			serialized.FindProperty("<MinutesPerStage>k__BackingField").intValue = minutesPerStage;
			serialized.FindProperty("<MaxStage>k__BackingField").intValue = maxStage;
			serialized.FindProperty("<MaxVitality>k__BackingField").floatValue = maxVitality;
			serialized.FindProperty("<DrainPerMinute>k__BackingField").floatValue = drainPerMinute;
			serialized.FindProperty("<TendRestore>k__BackingField").floatValue = tendRestore;
			serialized.ApplyModifiedProperties();
			return plant;
		}

		// CreateInstance 는 [field: SerializeField] 이니셜라이저를 직렬화 디폴트(0)로 덮음(Unity 현실).
		// 디자이너 안전망 = Reset()→ApplyDefaults(). 그게 sane 기본값을 보장하는지 검증.
		[Test]
		public void ApplyDefaults_GivesSaneParams()
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();

			plant.ApplyDefaults();
			PlantGrowthParams parameters = plant.ToGrowthParams();

			Assert.That(parameters.MinutesPerStage, Is.EqualTo(60), "기본 성장시간");
			Assert.That(parameters.MaxStage, Is.EqualTo(3));
			Assert.That(parameters.DrainPerMinute, Is.GreaterThan(0f), "마도작물 기본=시듦 있음");

			Object.DestroyImmediate(plant);
		}

		[Test]
		public void ToGrowthParams_CarriesSerializedValues()
		{
			WitchPlantSO plant = MakeSO(minutesPerStage: 30, maxStage: 5, maxVitality: 80f, drainPerMinute: 2f, tendRestore: 40f);

			PlantGrowthParams parameters = plant.ToGrowthParams();

			Assert.That(parameters.MinutesPerStage, Is.EqualTo(30));
			Assert.That(parameters.MaxStage, Is.EqualTo(5));
			Assert.That(parameters.MaxVitality, Is.EqualTo(80f));
			Assert.That(parameters.DrainPerMinute, Is.EqualTo(2f));
			Assert.That(parameters.TendRestore, Is.EqualTo(40f));

			Object.DestroyImmediate(plant);
		}

		[Test]
		public void SOParams_DriveGrowthModel_EndToEnd()
		{
			// SO → params → 실제 성장모델 구동: 마도작물 방치하면 시듦(통합 = 데드 SO 아님 증명).
			WitchPlantSO plant = MakeSO(minutesPerStage: 60, maxStage: 3, maxVitality: 10f, drainPerMinute: 1f, tendRestore: 5f);
			PlantGrowthParams parameters = plant.ToGrowthParams();
			PlantGrowthState state = new(parameters.MaxVitality);

			WitchPlantGrowth.Step(state, parameters, 20); // 생기 10 < 소모 20 → 시듦

			Assert.That(state.Withered, Is.True, "SO 수치가 성장모델을 실제로 구동");

			Object.DestroyImmediate(plant);
		}

		[Test]
		public void ZeroDrainSO_NeverWithers_CozyCrop()
		{
			// Drain 0 SO = 코지(일반작물 동등) — 절충 톤의 코드 표현.
			WitchPlantSO plant = MakeSO(minutesPerStage: 60, maxStage: 3, maxVitality: 100f, drainPerMinute: 0f, tendRestore: 0f);
			PlantGrowthParams parameters = plant.ToGrowthParams();
			PlantGrowthState state = new(parameters.MaxVitality);

			WitchPlantGrowth.Step(state, parameters, 1000);

			Assert.That(state.Withered, Is.False, "Drain 0 SO = 안 시듦(코지)");
			Assert.That(WitchPlantGrowth.IsHarvestable(state, parameters), Is.True, "시간 지나면 개화");

			Object.DestroyImmediate(plant);
		}

		// 변이(pillar 3) — 누가 길렀나(DominantCarerId)가 수확물을 가른다. 순수·결정적.
		[Test]
		public void ResolveCarerVariant_MatchesDominantCarer()
		{
			ItemData ring = ScriptableObject.CreateInstance<ItemData>();
			ItemData alisa = ScriptableObject.CreateInstance<ItemData>();
			List<CarerLoot> carerLoots = new() { new CarerLoot(0, ring), new CarerLoot(1, alisa) };

			Assert.That(WitchPlantSO.ResolveCarerVariant(carerLoots, true, 1), Is.SameAs(alisa), "dominant carer 1 → alisa 변이");
			Assert.That(WitchPlantSO.ResolveCarerVariant(carerLoots, true, 0), Is.SameAs(ring), "dominant carer 0 → ring 변이");
			Assert.That(WitchPlantSO.ResolveCarerVariant(carerLoots, true, 9), Is.Null, "매치 없는 carer → null(기본 추첨으로)");
			Assert.That(WitchPlantSO.ResolveCarerVariant(carerLoots, false, 0), Is.Null, "돌봄자 없음 → null");

			Object.DestroyImmediate(ring);
			Object.DestroyImmediate(alisa);
		}

		// 수확물 결정(pillar 1+3) — 변이 우선, 없으면 기본 추첨. 단일 100% 표로 결정성 확보.
		[Test]
		public void ResolveHarvestItem_VariantBeatsDefault_ElseDefault()
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			ItemData defaultLoot = ScriptableObject.CreateInstance<ItemData>();
			ItemData ringVariant = ScriptableObject.CreateInstance<ItemData>();

			plant.EditorSetHarvestLoots(new List<DataSOWithPercentage>
			{
				new DataSOWithPercentage { DataSO = defaultLoot, Percentage = 100f },
			});
			plant.EditorSetCarerLoots(new List<CarerLoot> { new CarerLoot(0, ringVariant) });

			Assert.That(plant.ResolveHarvestItem(true, 0), Is.SameAs(ringVariant), "carer 0 가 길렀으면 변이품");
			Assert.That(plant.ResolveHarvestItem(true, 5), Is.SameAs(defaultLoot), "변이 매치 없으면 기본(단일 100%)");
			Assert.That(plant.ResolveHarvestItem(false, -1), Is.SameAs(defaultLoot), "돌봄자 없으면 기본");

			Object.DestroyImmediate(plant);
			Object.DestroyImmediate(defaultLoot);
			Object.DestroyImmediate(ringVariant);
		}

		[Test]
		public void ResolveHarvestItem_NoLoots_ReturnsNull()
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();

			Assert.That(plant.ResolveHarvestItem(false, -1), Is.Null, "수확물 표 0개 = null(인벤토리 추가 skip)");

			Object.DestroyImmediate(plant);
		}
	}
}
