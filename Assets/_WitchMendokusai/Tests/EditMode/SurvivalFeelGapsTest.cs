using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 생존 던전 연출 공백 셋: 처치 X 표시 그림, 정예 체력 바 채움, 처치 조각 산포
	public class SurvivalFeelGapsTest
	{
		private static readonly string[] DIED_X_PREFABS =
		{
			"Assets/_WitchMendokusai/Domain/Doll/_Common/Player.prefab",
			"Assets/_WitchMendokusai/Domain/Doll/_Common/ArenaDoll.prefab",
		};

		private static readonly string[] HP_BAR_PREFABS =
		{
			"Assets/_WitchMendokusai/Domain/Doll/_Common/HPBar.prefab",
			"Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/MonsterBase.prefab",
			"Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/WildBeast_Test.prefab",
		};

		[TestCaseSource(nameof(DIED_X_PREFABS))]
		public void DiedX_renderers_have_sprite(string prefabPath)
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			Assert.IsNotNull(prefab, prefabPath);
			int checkedCount = 0;
			foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
			{
				if (child.name != "DiedX")
					continue;
				foreach (SpriteRenderer spriteRenderer in child.GetComponents<SpriteRenderer>())
				{
					Assert.IsNotNull(spriteRenderer.sprite, $"{prefabPath} {child.name}");
					checkedCount++;
				}
			}
			Assert.Greater(checkedCount, 0, prefabPath);
		}

		[TestCaseSource(nameof(HP_BAR_PREFABS))]
		public void Hp_bar_view_is_wired(string prefabPath)
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			Assert.IsNotNull(prefab, prefabPath);
			HpBarView[] views = prefab.GetComponentsInChildren<HpBarView>(true);
			Assert.AreEqual(1, views.Length, prefabPath);
			SerializedObject serialized = new(views[0]);
			Assert.IsNotNull(serialized.FindProperty("fill").objectReferenceValue, prefabPath);
			SpriteRenderer fillRenderer = (SpriteRenderer)serialized.FindProperty("fillRenderer").objectReferenceValue;
			Assert.IsNotNull(fillRenderer, prefabPath);
			Assert.IsNotNull(fillRenderer.sprite, prefabPath);
		}

		[Test]
		public void Monster_hp_bar_field_points_at_view()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HP_BAR_PREFABS[1]);
			SerializedObject serialized = new(prefab.GetComponent<MonsterObject>());
			Transform hpBar = (Transform)serialized.FindProperty("hpBar").objectReferenceValue;
			Assert.IsNotNull(hpBar.GetComponent<HpBarView>());
		}

		[TestCase(1f, 0f)]
		[TestCase(0.5f, -0.25f)]
		[TestCase(0f, -0.5f)]
		[TestCase(-1f, -0.5f)]
		[TestCase(2f, 0f)]
		public void Fill_offset_keeps_left_edge(float ratio, float expected)
			=> Assert.AreEqual(expected, HpBarLayout.FillOffset(ratio, 1f), 0.0001f);

		[Test]
		public void Fill_left_edge_stays_fixed_for_any_ratio()
		{
			const float FULL_WIDTH = 0.384f;
			float fullLeft = -FULL_WIDTH * 0.5f;
			for (float ratio = 0f; ratio <= 1f; ratio += 0.05f)
			{
				float left = HpBarLayout.FillOffset(ratio, FULL_WIDTH) - FULL_WIDTH * ratio * 0.5f;
				Assert.AreEqual(fullLeft, left, 0.0001f);
			}
		}

		// 몬스터 바 판은 Y 180도라 right 가 월드 -x, 카메라 right 는 월드 +x (2026-11-07 Play 실측)
		[Test]
		public void Flipped_bar_anchors_screen_left()
		{
			Assert.AreEqual(-1f, HpBarLayout.ScreenLeftSign(Vector3.left, Vector3.right));
			Assert.AreEqual(1f, HpBarLayout.ScreenLeftSign(Vector3.right, Vector3.right));
			Assert.AreEqual(-1f, HpBarLayout.ScreenLeftSign(new Vector3(-0.99f, 0f, -0.13f), Vector3.right));
		}

		[Test]
		public void Nan_ratio_is_empty() => Assert.AreEqual(0f, HpBarLayout.Clamp(float.NaN));

		[Test]
		public void Elite_bar_is_wider_and_thicker()
		{
			SurvivalEventTable table = AssetDatabase.LoadAssetAtPath<SurvivalEventTable>("Assets/_WitchMendokusai/Domain/Dungeon/Survival/SET_SlimeForest10.asset");
			Assert.Greater(table.Elite.HpBarScale, 1f);
			Assert.Greater(table.Elite.HpBarThickness, 1f);
		}

		[TestCase(6)]
		[TestCase(40)]
		[TestCase(120)]
		public void Shards_fill_every_angle_sector(int count)
		{
			HashSet<int> sectors = new();
			for (int i = 0; i < count; i++)
			{
				Vector3 direction = ShardSpread.Direction(i, count, 0.37f, 0.5f, 0.2f);
				float turn = Mathf.Repeat(Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) - 0.37f, 1f);
				sectors.Add(Mathf.Min(count - 1, Mathf.FloorToInt(turn * count)));
			}
			Assert.AreEqual(count, sectors.Count);
		}

		[Test]
		public void Shard_direction_is_unit_and_mostly_horizontal()
		{
			for (int i = 0; i < 12; i++)
			{
				Vector3 direction = ShardSpread.Direction(i, 12, 0f, 1f, 0.35f);
				Assert.AreEqual(1f, direction.magnitude, 0.0001f);
				Vector2 horizontal = new(direction.x, direction.z);
				Assert.Greater(horizontal.magnitude, 0.9f);
			}
		}

		[Test]
		public void Shard_speed_is_not_clamped_near_zero()
		{
			SurvivalFeelConfig config = AssetDatabase.LoadAssetAtPath<SurvivalFeelConfig>("Assets/_WitchMendokusai/Domain/Dungeon/Survival/SurvivalFeelConfig.asset");
			Assert.Greater(config.ShardDrag, 0f);
			FeelMoment kill = config.Kill;
			float meanSpeed = kill.ShardSpeed * 0.75f;
			float travel = meanSpeed / config.ShardDrag * (1f - Mathf.Exp(-config.ShardDrag * kill.ShardLife));
			Assert.Greater(travel, 0.6f, "처치 조각 평균 이동 거리");
		}
	}
}
