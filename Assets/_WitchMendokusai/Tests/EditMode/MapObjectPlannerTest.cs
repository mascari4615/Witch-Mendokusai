using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 맵 오브젝트 배치 예산과 자리 고르기, 자산 검사. 정본 memo wm/design/map-objects-20261119
	public class MapObjectPlannerTest
	{
		private const string BUDGET_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/MapObject/MOB_SlimeForest.asset";
		private const string DUNGEON_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Dungeon/D_0_슬라임숲.asset";
		private readonly List<Object> created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object instance in created)
				Object.DestroyImmediate(instance);
			created.Clear();
		}

		private MapObjectData MakeData(string name, int cost, float weight, Vector2Int countRange, float minSpacing = 0f, Vector2? spawnTime = null)
		{
			MapObjectData data = ScriptableObject.CreateInstance<MapObjectData>();
			data.name = name;
			created.Add(data);
			SerializedObject serialized = new(data);
			serialized.FindProperty("<DisplayName>k__BackingField").stringValue = name;
			serialized.FindProperty("<PlacementCost>k__BackingField").intValue = cost;
			serialized.FindProperty("<Weight>k__BackingField").floatValue = weight;
			serialized.FindProperty("<CountRange>k__BackingField").vector2IntValue = countRange;
			serialized.FindProperty("<MinSpacing>k__BackingField").floatValue = minSpacing;
			serialized.FindProperty("<SpawnTime>k__BackingField").vector2Value = spawnTime ?? Vector2.zero;
			serialized.FindProperty("<PlayerDistance>k__BackingField").vector2Value = new Vector2(5f, 10f);
			SerializedProperty effects = serialized.FindProperty("<Effects>k__BackingField");
			effects.arraySize = 1;
			SerializedProperty effect = effects.GetArrayElementAtIndex(0);
			effect.FindPropertyRelative("<Type>k__BackingField").enumValueIndex = (int)MapObjectEffectType.SpawnElite;
			effect.FindPropertyRelative("<Count>k__BackingField").intValue = 1;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return data;
		}

		private MapObjectBudget MakeBudget(int credits, params MapObjectData[] candidates)
		{
			MapObjectBudget budget = ScriptableObject.CreateInstance<MapObjectBudget>();
			budget.name = "TestBudget";
			created.Add(budget);
			SerializedObject serialized = new(budget);
			serialized.FindProperty("<Credits>k__BackingField").intValue = credits;
			SerializedProperty list = serialized.FindProperty("<Candidates>k__BackingField");
			list.arraySize = candidates.Length;
			for (int i = 0; i < candidates.Length; i++)
				list.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return budget;
		}

		private static string Signature(List<MapObjectPlan> plans) => string.Join(",", plans.Select(plan => $"{plan.Data.name}@{plan.SpawnTime:0.###}"));

		[Test]
		public void Same_seed_gives_same_plan()
		{
			MapObjectBudget budget = MakeBudget(100, MakeData("A", 10, 1f, new Vector2Int(0, 5), spawnTime: new Vector2(0f, 60f)), MakeData("B", 15, 2f, new Vector2Int(0, 5)), MakeData("C", 5, 0.5f, new Vector2Int(0, 5)));

			for (int seed = 0; seed < 20; seed++)
				Assert.AreEqual(Signature(MapObjectPlanner.Plan(budget, seed, MapObjectPacing.None, out _)), Signature(MapObjectPlanner.Plan(budget, seed, MapObjectPacing.None, out _)), $"시드 {seed}");
		}

		[Test]
		public void Different_seeds_vary_the_plan()
		{
			MapObjectBudget budget = MakeBudget(100, MakeData("A", 10, 1f, new Vector2Int(0, 5)), MakeData("B", 15, 2f, new Vector2Int(0, 5)), MakeData("C", 5, 0.5f, new Vector2Int(0, 5)));

			HashSet<string> signatures = new();
			for (int seed = 0; seed < 20; seed++)
				signatures.Add(Signature(MapObjectPlanner.Plan(budget, seed, MapObjectPacing.None, out _)));
			Assert.Greater(signatures.Count, 1);
		}

		[Test]
		public void Credits_and_count_caps_hold()
		{
			MapObjectData cheap = MakeData("Cheap", 3, 5f, new Vector2Int(0, 2));
			MapObjectData pricey = MakeData("Pricey", 20, 1f, new Vector2Int(0, 10));
			MapObjectBudget budget = MakeBudget(50, cheap, pricey);

			for (int seed = 0; seed < 50; seed++)
			{
				List<MapObjectPlan> plans = MapObjectPlanner.Plan(budget, seed, MapObjectPacing.None, out int remaining);
				int spent = plans.Sum(plan => plan.Data.PlacementCost);
				Assert.LessOrEqual(spent, 50);
				Assert.AreEqual(50 - spent, remaining);
				Assert.LessOrEqual(plans.Count(plan => plan.Data == cheap), 2);
				// 남은 크레딧으로 더 살 수 있는 후보가 없어야 끝
				bool canBuyCheap = remaining >= 3 && plans.Count(plan => plan.Data == cheap) < 2;
				bool canBuyPricey = remaining >= 20;
				Assert.IsFalse(canBuyCheap || canBuyPricey, $"시드 {seed} 예산 남김 {remaining}");
			}
		}

		[Test]
		public void Minimum_count_comes_first_even_with_zero_weight()
		{
			MapObjectData required = MakeData("Required", 10, 0f, new Vector2Int(1, 1));
			MapObjectData filler = MakeData("Filler", 1, 10f, new Vector2Int(0, 100));
			MapObjectBudget budget = MakeBudget(15, required, filler);

			List<MapObjectPlan> plans = MapObjectPlanner.Plan(budget, 3, MapObjectPacing.None, out int remaining);

			Assert.AreEqual(1, plans.Count(plan => plan.Data == required));
			Assert.AreEqual(5, plans.Count(plan => plan.Data == filler));
			Assert.AreEqual(0, remaining);
		}

		[Test]
		public void Plans_are_sorted_by_spawn_time()
		{
			MapObjectBudget budget = MakeBudget(100, MakeData("Late", 10, 1f, new Vector2Int(0, 5), spawnTime: new Vector2(100f, 200f)), MakeData("Early", 10, 1f, new Vector2Int(0, 5)));

			List<MapObjectPlan> plans = MapObjectPlanner.Plan(budget, 11, MapObjectPacing.None, out _);

			for (int i = 1; i < plans.Count; i++)
				Assert.LessOrEqual(plans[i - 1].SpawnTime, plans[i].SpawnTime);
			Assert.IsTrue(plans.Where(plan => plan.Data.name == "Late").All(plan => plan.SpawnTime >= 100f && plan.SpawnTime <= 200f));
		}

		[Test]
		public void Position_respects_spacing_and_distance()
		{
			MapObjectData data = MakeData("Spaced", 1, 1f, new Vector2Int(0, 10), minSpacing: 4f);
			List<Vector3> placed = new();
			System.Random random = new(5);
			MapObjectProbe flatGround = (Vector3 candidate, out Vector3 ground) =>
			{
				ground = candidate;
				return MapObjectProbeResult.Ok;
			};

			for (int i = 0; i < 6; i++)
			{
				if (MapObjectPlanner.TryPickPosition(random, data, Vector3.zero, placed, new List<Vector3>(), 0f, 64, flatGround, out Vector3 position, out _) == false)
					continue;
				float distance = new Vector2(position.x, position.z).magnitude;
				Assert.That(distance, Is.InRange(5f - 0.001f, 10f + 0.001f));
				foreach (Vector3 other in placed)
					Assert.GreaterOrEqual(Vector3.Distance(other, position), 4f);
				placed.Add(position);
			}
			Assert.Greater(placed.Count, 1);
		}

		[Test]
		public void Position_reports_failure_reasons()
		{
			MapObjectData data = MakeData("Blocked", 1, 1f, new Vector2Int(0, 1));
			MapObjectProbe noGround = (Vector3 candidate, out Vector3 ground) =>
			{
				ground = candidate;
				return MapObjectProbeResult.NoGround;
			};

			bool found = MapObjectPlanner.TryPickPosition(new System.Random(1), data, Vector3.zero, new List<Vector3>(), new List<Vector3>(), 0f, 8, noGround, out _, out MapObjectPlacementTally tally);

			Assert.IsFalse(found);
			Assert.AreEqual(8, tally.Attempts);
			Assert.AreEqual(8, tally.NoGround);
		}

		[Test]
		public void Shipped_map_object_assets_are_valid()
		{
			List<string> errors = new();
			MapObjectData[] all = MapObjectSettings.LoadAllData();
			Assert.IsNotEmpty(all);
			foreach (MapObjectData data in all)
				data.Validate(errors);
			MapObjectSettings.Load().Validate(errors);
			AssetDatabase.LoadAssetAtPath<MapObjectBudget>(BUDGET_PATH).Validate(errors);
			CollectionAssert.IsEmpty(errors);
		}

		[Test]
		public void Slime_forest_places_exactly_one_challenge_site()
		{
			Dungeon dungeon = AssetDatabase.LoadAssetAtPath<Dungeon>(DUNGEON_PATH);
			Assert.IsNotNull(dungeon.MapObjectBudget);

			List<MapObjectPlan> plans = MapObjectPlanner.Plan(dungeon.MapObjectBudget, 1, MapObjectPacing.None, out _);

			Assert.AreEqual(1, plans.Count(plan => plan.Data.Category == MapObjectCategory.Challenge));
			Assert.IsTrue(plans.Where(plan => plan.Data.Category == MapObjectCategory.Challenge).All(plan => plan.SpawnTime == 0f), "판 시작 배치");
		}

		[Test]
		public void Zero_count_effect_is_rejected()
		{
			MapObjectData data = MakeData("ZeroCount", 1, 1f, new Vector2Int(0, 1));
			SerializedObject serialized = new(data);
			serialized.FindProperty("<Effects>k__BackingField").GetArrayElementAtIndex(0).FindPropertyRelative("<Count>k__BackingField").intValue = 0;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			List<string> errors = new();

			data.Validate(errors);

			Assert.IsTrue(errors.Any(error => error.Contains("개수 0")));
		}

		[Test]
		public void Budget_rejects_minimums_it_cannot_buy()
		{
			MapObjectBudget budget = MakeBudget(15, MakeData("First", 10, 1f, new Vector2Int(1, 1)), MakeData("Second", 10, 1f, new Vector2Int(1, 1)));
			List<string> errors = new();

			budget.Validate(errors);

			Assert.IsTrue(errors.Any(error => error.Contains("최소 개수")));
		}

		[Test]
		public void Run_seed_branch_matches_current_run_overload()
		{
			// 개발자 창 시뮬레이션 (정해진 판 시드) 과 판 안 배치 (현재 판 시드) 가 같은 갈래
			Assert.AreEqual(RunSeed.Derive(RunSeed.Current, 7301), RunSeed.Derive(7301));
			Assert.AreNotEqual(RunSeed.Derive(1, 7301), RunSeed.Derive(2, 7301));
		}

		[Test]
		public void Validation_catches_bad_ranges()
		{
			MapObjectData data = MakeData("Bad", 1, 0f, new Vector2Int(2, 1));
			List<string> errors = new();

			data.Validate(errors);

			Assert.IsTrue(errors.Any(error => error.Contains(nameof(MapObjectData.CountRange))));
		}
	}
}
