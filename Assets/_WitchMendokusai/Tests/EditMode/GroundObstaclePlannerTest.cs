using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 막는 지형 장애물 배치 규칙: 간격, 시작 비움, 길 비움, 재현, 밀도, 몹 생성 밀어내기
	public class GroundObstaclePlannerTest
	{
		private const string ASSET_PATH = "Assets/_WitchMendokusai/Domain/World/Parts/Ground/Obstacle/GroundObstacles_Forest.asset";
		private const int RUN_SEED = 12345;
		private const int SPAN = 40;

		private GroundObstacleRules rules;
		private float[] radii;
		private readonly Vector2 start = Vector2.zero;

		[SetUp]
		public void SetUp()
		{
			GroundObstacleSettings settings = AssetDatabase.LoadAssetAtPath<GroundObstacleSettings>(ASSET_PATH);
			Assert.IsNotNull(settings, "GroundObstacles_Forest 자산 없음");
			rules = settings.Rules;
			radii = GroundObstaclePlanner.KindRadii(rules);
		}

		private List<GroundObstaclePlacement> Collect(int seed, int span)
		{
			List<GroundObstaclePlacement> result = new();
			for (int x = -span; x < span; x++)
				for (int z = -span; z < span; z++)
					if (GroundObstaclePlanner.TryPlace(rules, radii, seed, start, new Vector2Int(x, z), out GroundObstaclePlacement placement))
						result.Add(placement);
			return result;
		}

		[Test]
		public void Settings_are_valid()
		{
			List<string> errors = new();
			rules.Validate(errors);
			Assert.IsEmpty(errors, string.Join(", ", errors));
		}

		[Test]
		public void Obstacles_keep_min_spacing_and_leave_gap_for_body()
		{
			List<GroundObstaclePlacement> all = Collect(RUN_SEED, SPAN);
			Assert.Greater(all.Count, 100);
			float minGap = float.PositiveInfinity;
			Dictionary<Vector2Int, GroundObstaclePlacement> byCell = new();
			foreach (GroundObstaclePlacement placement in all)
				byCell[placement.Cell] = placement;
			foreach (GroundObstaclePlacement placement in all)
				for (int x = -1; x <= 1; x++)
					for (int z = -1; z <= 1; z++)
					{
						if ((x != 0 || z != 0) == false || byCell.TryGetValue(placement.Cell + new Vector2Int(x, z), out GroundObstaclePlacement other) == false)
							continue;
						float distance = Vector2.Distance(placement.Position, other.Position);
						Assert.GreaterOrEqual(distance, rules.MinSpacing - .001f);
						minGap = Mathf.Min(minGap, distance - placement.Radius - other.Radius);
					}
			// 몸 지름 1m 가 두 장애물 사이를 지나갈 수 있어야 함
			Assert.GreaterOrEqual(minGap, 1.2f, "장애물 사이 최소 틈 " + minGap);
		}

		[Test]
		public void Start_area_and_paths_are_clear()
		{
			foreach (GroundObstaclePlacement placement in Collect(RUN_SEED, SPAN))
			{
				Assert.GreaterOrEqual(placement.Position.magnitude, rules.StartClearRadius - .001f);
				Assert.IsFalse(GroundObstaclePlanner.InPath(rules, RUN_SEED, placement.Position), "길 안 장애물 " + placement.Position);
			}
		}

		[Test]
		public void Same_seed_same_layout_other_seed_differs()
		{
			List<GroundObstaclePlacement> first = Collect(RUN_SEED, 10);
			List<GroundObstaclePlacement> second = Collect(RUN_SEED, 10);
			Assert.AreEqual(first.Count, second.Count);
			for (int i = 0; i < first.Count; i++)
				Assert.AreEqual(first[i].Position, second[i].Position);
			List<GroundObstaclePlacement> other = Collect(RUN_SEED + 1, 10);
			Assert.IsFalse(first.Count == other.Count && first.Count > 0 && first[0].Position == other[0].Position);
		}

		[Test]
		public void Density_report_per_24m_chunk_and_per_225m2()
		{
			int extent = 60;
			List<GroundObstaclePlacement> all = Collect(RUN_SEED, extent);
			float area = (extent * 2f * rules.CellSize) * (extent * 2f * rules.CellSize);
			float perChunk = all.Count / area * 24f * 24f;
			float per225 = all.Count / area * 225f;
			Debug.Log($"[ObstacleDensity] 총 {all.Count}개 / {area:F0}m2, 24m 칸당 {perChunk:F2}, 225m2 당 {per225:F2}");
			Assert.Greater(perChunk, 1f);
			Assert.Less(perChunk, 12f);
		}

		[Test]
		public void PushOut_moves_spawn_out_of_obstacle()
		{
			List<GroundObstaclePlacement> all = Collect(RUN_SEED, 10);
			Assert.Greater(all.Count, 0);
			foreach (GroundObstaclePlacement placement in all)
			{
				Vector2 moved = GroundObstaclePlanner.PushOut(rules, radii, RUN_SEED, start, placement.Position, .5f);
				Assert.IsFalse(GroundObstaclePlanner.TryNearest(rules, radii, RUN_SEED, start, moved, .5f, out _), "밀어낸 자리에도 장애물 " + moved);
			}
		}
	}
}
