using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 맵 오브젝트 3단계 위협 (th2 경계, th4 생성과 반납, th5 고정 고리, th6 공격, th8 횡단). 정본 memo wm/design/map-objects-20261119/decisions.md
	public class SurvivalThreatTest
	{
		private const string DUNGEON_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Dungeon/D_0_슬라임숲.asset";
		private const string THREAT_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/SlimeGroup/Threat/";
		private const string PATTERN_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Dungeon/MonsterWave/Pattern/";
		private readonly List<Object> created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object instance in created)
				Object.DestroyImmediate(instance);
			created.Clear();
		}

		[Test]
		public void Forward_cone_stays_ahead_while_moving()
		{
			for (int i = 0; i <= 10; i++)
			{
				float angle = SurvivalThreatRules.PickSpawnAngle(SpawnPlacementPolicy.ForwardCone, true, 30f, 0f, 0.99, i / 10.0, out bool relative);
				Assert.IsTrue(relative);
				Assert.That(angle, Is.InRange(-30f, 30f));
			}
			SurvivalThreatRules.PickSpawnAngle(SpawnPlacementPolicy.ForwardCone, false, 30f, 0f, 0.0, 0.5, out bool standingRelative);
			Assert.IsFalse(standingRelative, "멈추면 둘레");
		}

		[Test]
		public void Offscreen_ring_uses_cone_by_weight()
		{
			SurvivalThreatRules.PickSpawnAngle(SpawnPlacementPolicy.OffscreenRing, true, 30f, 0.5f, 0.2, 0.5, out bool coneRelative);
			Assert.IsTrue(coneRelative, "굴림이 가중치 아래면 앞 부채꼴");
			float angle = SurvivalThreatRules.PickSpawnAngle(SpawnPlacementPolicy.OffscreenRing, true, 30f, 0.5f, 0.7, 0.75, out bool ringRelative);
			Assert.IsFalse(ringRelative, "가중치 위면 둘레");
			Assert.AreEqual(270f, angle, 0.001f);
			SurvivalThreatRules.PickSpawnAngle(SpawnPlacementPolicy.OffscreenRing, true, 30f, 0f, 0.0, 0.5, out bool zeroWeight);
			Assert.IsFalse(zeroWeight, "가중치 0 이면 늘 둘레");
		}

		[TestCase(ArenaBoundaryShape.Circle)]
		[TestCase(ArenaBoundaryShape.Square)]
		public void Arena_clamp_pulls_inside_and_keeps_height(ArenaBoundaryShape shape)
		{
			Vector3 center = new(5f, 1f, -3f);
			Vector3 inside = center + new Vector3(3f, 0f, 2f);
			Assert.AreEqual(inside, SurvivalThreatRules.Clamp(shape, center, 20f, inside, 1f));
			Vector3 outside = center + new Vector3(40f, 2f, 25f);
			Vector3 clamped = SurvivalThreatRules.Clamp(shape, center, 20f, outside, 1f);
			Assert.IsTrue(SurvivalThreatRules.Contains(shape, center, 20f, clamped, 1f - 0.001f), $"{clamped}");
			Assert.IsFalse(SurvivalThreatRules.Contains(shape, center, 20f, outside, 0f));
			Assert.AreEqual(outside.y, clamped.y);
		}

		[Test]
		public void Circle_walls_close_the_boundary()
		{
			const float HALF_SIZE = 30f;
			const float THICKNESS = 1f;
			const int SEGMENTS = 48;
			List<ArenaWallSegment> segments = SurvivalThreatRules.WallSegments(ArenaBoundaryShape.Circle, HALF_SIZE, SEGMENTS, THICKNESS);
			Assert.AreEqual(SEGMENTS, segments.Count);
			float chord = 2f * (HALF_SIZE + THICKNESS * 0.5f) * Mathf.Sin(Mathf.PI / SEGMENTS);
			foreach (ArenaWallSegment segment in segments)
			{
				Assert.AreEqual(HALF_SIZE + THICKNESS * 0.5f, segment.Offset.magnitude, 0.001f);
				Assert.Greater(segment.Length, chord, "이웃 조각과 이어짐");
				// 조각 길이 축 (로컬 X) 이 반지름과 수직
				Vector3 lengthAxis = Quaternion.Euler(0f, segment.Yaw, 0f) * Vector3.right;
				Assert.AreEqual(0f, Vector3.Dot(lengthAxis, segment.Offset.normalized), 0.001f);
			}
		}

		[Test]
		public void Square_walls_face_each_side()
		{
			List<ArenaWallSegment> segments = SurvivalThreatRules.WallSegments(ArenaBoundaryShape.Square, 20f, 64, 1f);
			Assert.AreEqual(4, segments.Count);
			foreach (ArenaWallSegment segment in segments)
			{
				Vector3 lengthAxis = Quaternion.Euler(0f, segment.Yaw, 0f) * Vector3.right;
				Assert.AreEqual(0f, Vector3.Dot(lengthAxis, segment.Offset.normalized), 0.001f);
				Assert.AreEqual(42f, segment.Length, 0.001f);
			}
			Assert.AreEqual(0, SurvivalThreatRules.WallSegments(ArenaBoundaryShape.None, 20f, 64, 1f).Count);
		}

		[Test]
		public void Arena_validation_rejects_inset_beyond_size()
		{
			DungeonArenaOptions arena = new DungeonArenaOptions().With(ArenaBoundaryShape.Circle, ArenaBoundaryMode.Wall, 1f);
			List<string> errors = new();
			arena.Validate("Test", errors);
			Assert.AreEqual(2, errors.Count, "최소 반경 미만, 들임이 반경 이상: " + string.Join(", ", errors));
			errors.Clear();
			new DungeonArenaOptions().Validate("Off", errors);
			Assert.AreEqual(0, errors.Count, "꺼진 경계는 검사 안 함");
		}

		[Test]
		public void Policy_copy_keeps_other_fields_and_rejects_far_relocation()
		{
			DungeonThreatPolicy source = new();
			DungeonThreatPolicy copy = source.With(SpawnPlacementPolicy.ForwardCone, OffscreenRetirePolicy.Relocate);
			Assert.AreEqual(SpawnPlacementPolicy.OffscreenRing, source.SpawnPolicy, "원본은 그대로");
			Assert.AreEqual(SpawnPlacementPolicy.ForwardCone, copy.SpawnPolicy);
			Assert.AreEqual(OffscreenRetirePolicy.Relocate, copy.RetirePolicy);
			Assert.AreEqual(source.OffscreenMaxDistance, copy.OffscreenMaxDistance);
			List<string> errors = new();
			copy.Validate("Test", copy.OffscreenMaxDistance, errors);
			Assert.AreEqual(1, errors.Count, "재배치 거리가 반납 거리 이상이면 매 틱 반복");
		}

		[Test]
		public void Move_velocity_follows_input_direction()
		{
			Vector3 velocity = SurvivalThreatRules.MoveVelocity(new Vector3(1f, 0f, 0f), new Vector3(0f, -3f, 2f));
			Assert.AreEqual(new Vector3(2f, 0f, 0f), velocity, "방향은 입력, 크기는 수평 속도");
			Assert.AreEqual(Vector3.zero, SurvivalThreatRules.MoveVelocity(Vector3.zero, new Vector3(5f, 0f, 0f)), "입력 없으면 넉백 속도 무시");
		}

		[Test]
		public void Slime_forest_threat_setup_is_valid()
		{
			Dungeon dungeon = AssetDatabase.LoadAssetAtPath<Dungeon>(DUNGEON_PATH);
			Assert.IsNotNull(dungeon);
			Assert.DoesNotThrow(dungeon.ValidateWaves);
			Assert.IsFalse(dungeon.Arena.Enabled, "경계는 선택 옵션, 슬라임숲 기본 무한");
			Assert.AreEqual(SpawnPlacementPolicy.OffscreenRing, dungeon.ThreatPolicy.SpawnPolicy, "기본 생성 정책은 화면 밖 둘레 (30도 카메라, 2026-11-22)");
			Assert.AreEqual(OffscreenRetirePolicy.Retire, dungeon.ThreatPolicy.RetirePolicy, "기본 반납 정책은 기존 그대로");
			List<string> patternNames = new();
			foreach (WavePatternSlot slot in dungeon.WavePatterns)
				patternNames.Add(slot.Pattern.name);
			CollectionAssert.Contains(patternNames, "WP_Crossing_Slime");
			CollectionAssert.Contains(patternNames, "WP_FixedRing_Slime");
			HashSet<System.Type> attackTypes = new();
			foreach (MonsterWave wave in dungeon.MonsterWaves)
				foreach (Monster monster in wave.Monsters)
					if (monster.Attack != null)
						attackTypes.Add(monster.Attack.GetType());
			CollectionAssert.AreEquivalent(new[] { typeof(RangedMonsterAttack), typeof(DashMonsterAttack), typeof(ZoneMonsterAttack) }, attackTypes);
		}

		[Test]
		public void Threat_patterns_are_crossing_band_and_fixed_ring()
		{
			MovingWallPattern crossing = AssetDatabase.LoadAssetAtPath<MovingWallPattern>(PATTERN_FOLDER + "WP_Crossing_Slime.asset");
			Assert.AreEqual(WallDirectionMode.AcrossPlayerMove, crossing.DirectionMode);
			Assert.IsFalse(crossing.BothSides, "한쪽에서만");
			Assert.Greater(crossing.WarnSeconds, 0f, "지나갈 길 예고");
			Assert.IsNotNull(crossing.WarnTelegraph);
			EllipseSiegePattern ring = AssetDatabase.LoadAssetAtPath<EllipseSiegePattern>(PATTERN_FOLDER + "WP_FixedRing_Slime.asset");
			Assert.IsFalse(ring.FollowPlayer, "고리 중심은 시작 자리에 고정");
			Assert.Greater(ring.ShrinkPerSecond, 0f);
		}

		[Test]
		public void Moving_wall_warning_needs_telegraph()
		{
			MovingWallPattern pattern = ScriptableObject.CreateInstance<MovingWallPattern>();
			pattern.name = "Warn";
			created.Add(pattern);
			SerializedObject serialized = new(pattern);
			serialized.FindProperty("<Monsters>k__BackingField").arraySize = 1;
			serialized.FindProperty("<Monsters>k__BackingField").GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Monster>(THREAT_FOLDER + "MOB_13_SlimeCharger.asset");
			serialized.FindProperty("<WarnSeconds>k__BackingField").floatValue = 2f;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			Assert.Throws<System.InvalidOperationException>(pattern.Validate);
		}

		[Test]
		public void Attack_validation_catches_bad_setup()
		{
			Monster shooter = AssetDatabase.LoadAssetAtPath<Monster>(THREAT_FOLDER + "MOB_12_SlimeShooter.asset");
			Monster charger = AssetDatabase.LoadAssetAtPath<Monster>(THREAT_FOLDER + "MOB_13_SlimeCharger.asset");

			RangedMonsterAttack ranged = ScriptableObject.CreateInstance<RangedMonsterAttack>();
			ranged.name = "Ranged";
			created.Add(ranged);
			SerializedObject rangedSerialized = new(ranged);
			rangedSerialized.FindProperty("<WindupSeconds>k__BackingField").floatValue = 0f;
			rangedSerialized.FindProperty("<SkillIndex>k__BackingField").intValue = 3;
			rangedSerialized.ApplyModifiedPropertiesWithoutUndo();
			List<string> errors = new();
			ranged.Validate(shooter, errors);
			Assert.AreEqual(1, errors.Count, "없는 스킬 번호: " + string.Join(", ", errors));

			DashMonsterAttack dash = ScriptableObject.CreateInstance<DashMonsterAttack>();
			dash.name = "Dash";
			created.Add(dash);
			errors.Clear();
			dash.Validate(charger, errors);
			Assert.AreEqual(1, errors.Count, "예고 초가 있는데 예고 부품 없음: " + string.Join(", ", errors));

			ZoneMonsterAttack zone = ScriptableObject.CreateInstance<ZoneMonsterAttack>();
			zone.name = "Zone";
			created.Add(zone);
			SerializedObject zoneSerialized = new(zone);
			zoneSerialized.FindProperty("<WindupSeconds>k__BackingField").floatValue = 0f;
			zoneSerialized.ApplyModifiedPropertiesWithoutUndo();
			errors.Clear();
			zone.Validate(charger, errors);
			Assert.AreEqual(1, errors.Count, "예고 없는 장판: " + string.Join(", ", errors));
		}

		[Test]
		public void Zone_target_leads_player_motion()
		{
			ZoneMonsterAttack zone = AssetDatabase.LoadAssetAtPath<ZoneMonsterAttack>(THREAT_FOLDER + "MA_Zone_Slime.asset");
			Vector3 target = zone.ResolveTarget(new Vector3(1f, 0f, 1f), new Vector3(2f, 5f, 0f));
			Assert.AreEqual(1f + 2f * zone.LeadSeconds, target.x, 0.001f);
			Assert.AreEqual(0f, target.y, 0.001f, "높이 속도는 무시");
		}
	}
}
