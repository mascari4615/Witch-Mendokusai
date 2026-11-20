using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 맵 오브젝트 2단계 규칙 (머무름, 비용, 운, 데이터 검사). 정본 memo wm/design/map-objects-20261119/decisions.md
	public class MapObjectRulesTest
	{
		private const string BUDGET_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/MapObject/MOB_SlimeForest.asset";
		private readonly List<Object> created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object instance in created)
				Object.DestroyImmediate(instance);
			created.Clear();
		}

		private MapObjectData MakeData(MapObjectCostType costType, float costValue, MapObjectEffectType effectType, MapObjectEffectTiming timing, float effectValue, float holdSeconds)
		{
			MapObjectData data = ScriptableObject.CreateInstance<MapObjectData>();
			data.name = "Rule";
			created.Add(data);
			SerializedObject serialized = new(data);
			serialized.FindProperty("<DisplayName>k__BackingField").stringValue = "Rule";
			serialized.FindProperty("<CostType>k__BackingField").enumValueIndex = (int)costType;
			serialized.FindProperty("<CostValue>k__BackingField").floatValue = costValue;
			serialized.FindProperty("<HoldSeconds>k__BackingField").floatValue = holdSeconds;
			serialized.FindProperty("<CountRange>k__BackingField").vector2IntValue = new Vector2Int(0, 1);
			serialized.FindProperty("<PlayerDistance>k__BackingField").vector2Value = new Vector2(5f, 10f);
			SerializedProperty effects = serialized.FindProperty("<Effects>k__BackingField");
			effects.arraySize = 1;
			SerializedProperty effect = effects.GetArrayElementAtIndex(0);
			effect.FindPropertyRelative("<Type>k__BackingField").enumValueIndex = (int)effectType;
			effect.FindPropertyRelative("<Timing>k__BackingField").enumValueIndex = (int)timing;
			effect.FindPropertyRelative("<Value>k__BackingField").floatValue = effectValue;
			effect.FindPropertyRelative("<Count>k__BackingField").intValue = 1;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return data;
		}

		private static List<string> ErrorsOf(MapObjectData data)
		{
			List<string> errors = new();
			data.Validate(errors);
			return errors;
		}

		[Test]
		public void Hold_progresses_inside_and_completes()
		{
			float progress = 0f;
			float age = 0f;
			MapObjectHoldResult result = MapObjectHoldResult.Running;
			for (int i = 0; i < 9 && result == MapObjectHoldResult.Running; i++)
				result = MapObjectRules.StepHold(ref progress, ref age, 0.5f, true, 4f, MapObjectHoldOutside.Pause, 1f, 0f);

			Assert.AreEqual(MapObjectHoldResult.Completed, result);
			Assert.AreEqual(4f, progress, 0.0001f);
			Assert.AreEqual(4f, age, 0.0001f);
		}

		[Test]
		public void Hold_pauses_or_decays_outside()
		{
			float paused = 2f;
			float decayed = 2f;
			float age = 0f;
			MapObjectRules.StepHold(ref paused, ref age, 1f, false, 10f, MapObjectHoldOutside.Pause, 0.5f, 0f);
			MapObjectRules.StepHold(ref decayed, ref age, 1f, false, 10f, MapObjectHoldOutside.Decay, 0.5f, 0f);
			Assert.AreEqual(2f, paused, 0.0001f);
			Assert.AreEqual(1.5f, decayed, 0.0001f);

			float floor = 0.2f;
			MapObjectRules.StepHold(ref floor, ref age, 1f, false, 10f, MapObjectHoldOutside.Decay, 0.5f, 0f);
			Assert.AreEqual(0f, floor, "0 아래로 안 내려감");
		}

		[Test]
		public void Hold_fails_at_time_limit_unless_complete()
		{
			float progress = 0f;
			float age = 0f;
			Assert.AreEqual(MapObjectHoldResult.Running, MapObjectRules.StepHold(ref progress, ref age, 4f, false, 5f, MapObjectHoldOutside.Pause, 1f, 6f));
			Assert.AreEqual(MapObjectHoldResult.Failed, MapObjectRules.StepHold(ref progress, ref age, 2f, false, 5f, MapObjectHoldOutside.Pause, 1f, 6f));

			float finishing = 4f;
			float finishingAge = 5.5f;
			Assert.AreEqual(MapObjectHoldResult.Completed, MapObjectRules.StepHold(ref finishing, ref finishingAge, 1f, true, 5f, MapObjectHoldOutside.Pause, 1f, 6f), "같은 틱이면 완료가 먼저");
		}

		[Test]
		public void Scaled_cost_grows_and_rounds_up()
		{
			Assert.AreEqual(10, MapObjectRules.ScaledCost(10f, MapObjectRules.CostMultiplier(1.5f, 0)));
			Assert.AreEqual(15, MapObjectRules.ScaledCost(10f, MapObjectRules.CostMultiplier(1.5f, 1)));
			Assert.AreEqual(23, MapObjectRules.ScaledCost(10f, MapObjectRules.CostMultiplier(1.5f, 2)));
			Assert.AreEqual(0, MapObjectRules.ScaledCost(0f, MapObjectRules.CostMultiplier(2f, 3)));
			Assert.AreEqual(1, MapObjectRules.ScaledCost(0.2f, 1f), "값이 있으면 최소 1");
		}

		[Test]
		public void Health_cost_leaves_one_hp()
		{
			Assert.AreEqual(6, MapObjectRules.HealthCost(20, 30f, 1f));
			Assert.AreEqual(1, MapObjectRules.HealthCost(3, 10f, 1f), "올림, 최소 1");
			Assert.AreEqual(9, MapObjectRules.HealthCost(20, 30f, 1.5f), "재사용 배율은 % 에 곱함");
			Assert.IsTrue(MapObjectRules.CanPayHealth(7, 6));
			Assert.IsFalse(MapObjectRules.CanPayHealth(6, 6), "체력 0 이 되는 지불은 불가");
			Assert.AreEqual(8, MapObjectRules.HealAmount(20, 40f));
		}

		[Test]
		public void Roll_respects_fail_percent()
		{
			Assert.IsTrue(MapObjectRules.RollSucceeds(0f, 0.0));
			Assert.IsFalse(MapObjectRules.RollSucceeds(60f, 0.59));
			Assert.IsTrue(MapObjectRules.RollSucceeds(60f, 0.6));

			System.Random random = new(3);
			int wins = Enumerable.Range(0, 10000).Count(_ => MapObjectRules.RollSucceeds(60f, random.NextDouble()));
			Assert.That(wins, Is.InRange(3700, 4300));
		}

		[Test]
		public void Supported_costs_and_effects_cover_stage_two()
		{
			Assert.IsTrue(MapObjectRules.IsCostSupported(MapObjectCostType.HealthPercent));
			Assert.IsTrue(MapObjectRules.IsCostSupported(MapObjectCostType.RunNyang));
			Assert.IsFalse(MapObjectRules.IsCostSupported(MapObjectCostType.Card), "카드 비용은 판정 대기");
			Assert.IsFalse(MapObjectRules.IsCostSupported(MapObjectCostType.MaxHealthPercent));
			Assert.IsTrue(MapObjectRules.IsEffectSupported(MapObjectEffectType.Heal));
			Assert.IsTrue(MapObjectRules.IsEffectSupported(MapObjectEffectType.RevealMapObjects));
		}

		[Test]
		public void Validation_rejects_bad_stage_two_data()
		{
			Assert.IsEmpty(ErrorsOf(MakeData(MapObjectCostType.RunNyang, 10f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnActivate, 0f, 0f)));
			Assert.IsEmpty(ErrorsOf(MakeData(MapObjectCostType.None, 0f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnHoldComplete, 0f, 20f)));

			Assert.IsTrue(ErrorsOf(MakeData(MapObjectCostType.None, 0f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnHoldComplete, 0f, 0f)).Any(error => error.Contains("머무름 완료")));
			Assert.IsTrue(ErrorsOf(MakeData(MapObjectCostType.None, 0f, MapObjectEffectType.Heal, MapObjectEffectTiming.OnActivate, 0f, 0f)).Any(error => error.Contains("회복")));
			Assert.IsTrue(ErrorsOf(MakeData(MapObjectCostType.HealthPercent, 100f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnActivate, 0f, 0f)).Any(error => error.Contains("낼 수 없음")));
			Assert.IsTrue(ErrorsOf(MakeData(MapObjectCostType.RunNyang, 0f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnActivate, 0f, 0f)).Any(error => error.Contains("비용")));
			Assert.IsTrue(ErrorsOf(MakeData(MapObjectCostType.Card, 1f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnActivate, 0f, 0f)).Any(error => error.Contains("미지원")));
			Assert.IsTrue(ErrorsOf(MakeData(MapObjectCostType.None, 0f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnActivate, 0f, 20f)).Any(error => error.Contains("완료 효과 없음")));
		}

		[Test]
		public void Validation_rejects_health_cost_that_outgrows_hp()
		{
			MapObjectData data = MakeData(MapObjectCostType.HealthPercent, 40f, MapObjectEffectType.SpawnChest, MapObjectEffectTiming.OnActivate, 0f, 0f);
			SerializedObject serialized = new(data);
			serialized.FindProperty("<CostGrowth>k__BackingField").floatValue = 2f;
			serialized.FindProperty("<Uses>k__BackingField").intValue = 2;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			Assert.IsFalse(ErrorsOf(data).Any(error => error.Contains("100% 이상")), "두 번째 사용 40 x 2 = 80%");

			serialized.FindProperty("<Uses>k__BackingField").intValue = 3;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			Assert.IsTrue(ErrorsOf(data).Any(error => error.Contains("100% 이상")), "세 번째 사용 160%");
		}

		[Test]
		public void Slime_forest_always_has_scanner_and_challenge()
		{
			MapObjectBudget budget = AssetDatabase.LoadAssetAtPath<MapObjectBudget>(BUDGET_PATH);
			HashSet<MapObjectCategory> seen = new();
			for (int seed = 0; seed < 40; seed++)
			{
				List<MapObjectPlan> plans = MapObjectPlanner.Plan(budget, seed, out _);
				Assert.AreEqual(1, plans.Count(plan => plan.Data.Category == MapObjectCategory.Info), $"시드 {seed} 스캐너");
				Assert.AreEqual(1, plans.Count(plan => plan.Data.Category == MapObjectCategory.Challenge), $"시드 {seed} 도전");
				foreach (MapObjectPlan plan in plans)
					seen.Add(plan.Data.Category);
			}
			// 판마다 다른 조합이어도 40판 안에 2단계 분류가 전부 나옴
			CollectionAssert.IsSubsetOf(new[] { MapObjectCategory.HoldZone, MapObjectCategory.Exchange, MapObjectCategory.Luck, MapObjectCategory.HealSpring }, seen);
		}

		[Test]
		public void Every_reward_preview_has_a_label()
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			foreach (MapObjectData data in MapObjectSettings.LoadAllData())
			{
				if (data.RewardPreview == MapObjectRewardPreview.None)
					continue;
				Assert.IsTrue(settings.TryGetRewardPreview(data.RewardPreview, out MapObjectRewardPreviewStyle style), $"{data.name} {data.RewardPreview}");
				Assert.IsNotEmpty(style.Label);
			}
		}
	}
}
