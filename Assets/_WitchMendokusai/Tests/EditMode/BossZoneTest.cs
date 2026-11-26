using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 보스 판정 부품 다섯 (안전 섬, 빈칸 벽, 따라 도는 조준, 엄폐, 시선) 과 보스 더미 971~975. 정본 memo wm/design/boss-zones-20261126
	// 판정 계산마다 한 시험, 더한 칸의 기본값이 지금 동작과 같은지 (기존 자산), 더미 연결
	public class BossZoneTest
	{
		private const string DUMMY_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/ZoneDummy";
		private const string ADDRESS_GROUP = "Assets/AddressableAssetsData/AssetGroups/Monster.asset";
		private static readonly int[] dummyIds = { 971, 972, 973, 974, 975 };

		private static List<Monster> LoadDummies() =>
			AssetDatabase.FindAssets("t:Monster", new[] { DUMMY_FOLDER })
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.OrderBy(monster => monster.ID)
				.ToList();

		private static Monster Dummy(int id) => LoadDummies().Single(monster => monster.ID == id);

		private static List<T> LoadAll<T>() where T : Object =>
			AssetDatabase.FindAssets("t:" + typeof(T).Name)
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.Where(path => path.StartsWith(DUMMY_FOLDER) == false)
				.Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
				.ToList();

		// ---- 더미

		[Test]
		public void Every_zone_dummy_exists_with_an_address_and_no_loot()
		{
			List<Monster> dummies = LoadDummies();
			CollectionAssert.AreEquivalent(dummyIds, dummies.Select(monster => monster.ID));
			string group = File.ReadAllText(ADDRESS_GROUP).Replace("\r\n", "\n");
			foreach (Monster monster in dummies)
			{
				string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(monster));
				StringAssert.Contains($"m_GUID: {guid}\n    m_Address: Monster/{monster.ID}\n", group, monster.name);
				Assert.IsEmpty(monster.Loots, $"{monster.name} 더미는 드랍 없음");
			}
		}

		[Test]
		public void Zone_dummy_attacks_pass_validation_with_body_motion_and_strike_effect()
		{
			foreach (Monster monster in LoadDummies())
			{
				Assert.IsNotNull(monster.Attack, $"{monster.name} 공격 없음");
				List<string> errors = new();
				monster.Attack.Validate(monster, errors);
				Assert.IsEmpty(errors, string.Join("\n", errors));
				Assert.IsFalse(monster.Attack.Pose.IsEmpty, $"{monster.name} 몸동작 없음");
				Assert.IsNotNull(monster.Attack.StrikeFx, $"{monster.name} 타격 이펙트 없음");
			}
		}

		[Test]
		public void Each_dummy_uses_its_own_part()
		{
			Assert.IsInstanceOf<BandMonsterAttack>(Dummy(971).Attack);
			Assert.IsInstanceOf<ScatterZoneMonsterAttack>(Dummy(972).Attack);
			Assert.IsInstanceOf<LineMonsterAttack>(Dummy(973).Attack);
			Assert.IsInstanceOf<BandMonsterAttack>(Dummy(974).Attack);
			Assert.IsInstanceOf<GazeMonsterAttack>(Dummy(975).Attack);
			Assert.Greater(((BandMonsterAttack)Dummy(971).Attack).CenterOffset, 0f, "안전 섬은 주인공에서 떨어진 자리");
			Assert.AreNotEqual(ScatterGap.None, ((ScatterZoneMonsterAttack)Dummy(972).Attack).Gap);
			Assert.Greater(((LineMonsterAttack)Dummy(973).Attack).TrackSeconds, 0f);
			Assert.AreNotEqual(0, ((BandMonsterAttack)Dummy(974).Attack).CoverMask.value);
		}

		// ---- 기존 자산은 더한 칸의 기본값 (지금 동작) 그대로

		[Test]
		public void Existing_band_scatter_line_assets_keep_new_fields_at_defaults()
		{
			List<BandMonsterAttack> bands = LoadAll<BandMonsterAttack>();
			List<ScatterZoneMonsterAttack> scatters = LoadAll<ScatterZoneMonsterAttack>();
			List<LineMonsterAttack> lines = LoadAll<LineMonsterAttack>();
			Assert.IsNotEmpty(bands);
			Assert.IsNotEmpty(scatters);
			Assert.IsNotEmpty(lines);
			foreach (BandMonsterAttack band in bands)
			{
				Assert.AreEqual(0f, band.CenterOffset, band.name);
				Assert.AreEqual(0, band.CoverMask.value, band.name);
				Assert.IsNull(band.SafeFx, band.name);
			}
			foreach (ScatterZoneMonsterAttack scatter in scatters)
			{
				Assert.AreEqual(ScatterGap.None, scatter.Gap, scatter.name);
				Assert.AreEqual(1, scatter.Rows, scatter.name);
				Assert.AreNotEqual(ScatterArrangement.Across, scatter.Arrangement, scatter.name);
				Assert.IsNull(scatter.SafeFx, scatter.name);
			}
			foreach (LineMonsterAttack line in lines)
			{
				Assert.AreEqual(0f, line.TrackingSeconds, line.name);
				Assert.IsNull(line.TrackingLockFx, line.name);
			}
		}

		[Test]
		public void Default_fields_resolve_targets_like_before()
		{
			BandMonsterAttack band = ScriptableObject.CreateInstance<BandMonsterAttack>();
			ScatterZoneMonsterAttack scatter = ScriptableObject.CreateInstance<ScatterZoneMonsterAttack>();
			LineMonsterAttack line = ScriptableObject.CreateInstance<LineMonsterAttack>();
			try
			{
				Vector3 player = new(3f, 0f, 4f);
				// 띠: 중심 옮김 0 이면 주인공 자리 그대로, 엄폐 층이 비면 막는 것 없음, 꽉 찬 원이면 안전 자리 없음
				Assert.AreEqual(0f, band.CenterOffset);
				Assert.AreEqual(player, BandMonsterAttack.OffsetCenter(player, band.CenterOffset, 123f));
				Assert.AreEqual(0, band.CoverMask.value);
				Assert.IsFalse(MonsterCover.Blocks(Vector3.zero, player, band.CoverHeight, band.CoverMask, null, null));
				Assert.AreEqual(0f, band.SafeRadius);
				// 따라 돌기를 안 고친 종류는 목표 그대로
				Assert.AreEqual(Vector3.right, band.Track(null, player, Vector3.right, 0, 1f));
				// 여러 자리: 빈칸 없음, 줄 수 1
				Assert.AreEqual(ScatterGap.None, scatter.Gap);
				Assert.AreEqual(1, scatter.Rows);
				// 직선: 따라 돌지 않음
				Assert.AreEqual(0f, line.TrackingSeconds);
				Assert.IsNull(line.TrackingLockFx);
				// 안전 자리 표시는 비어 있음 (박자가 아무것도 더 띄우지 않음)
				foreach (WindupMonsterAttack attack in new WindupMonsterAttack[] { band, scatter, line })
					Assert.IsNull(attack.SafeFx);
			}
			finally
			{
				Object.DestroyImmediate(band);
				Object.DestroyImmediate(scatter);
				Object.DestroyImmediate(line);
			}
		}

		// ---- 1. 안전 섬

		[Test]
		public void Safe_island_hits_everywhere_outside_and_spares_the_island()
		{
			BandMonsterAttack island = (BandMonsterAttack)Dummy(971).Attack;
			AttackBand band = island.Bands[0];
			Vector3 player = Vector3.zero;
			Vector3 center = BandMonsterAttack.OffsetCenter(player, island.CenterOffset, 90f);
			Assert.AreEqual(island.CenterOffset, Vector3.Distance(player, center), 1e-4f);
			Assert.AreEqual(island.SafeRadius, band.Inner);
			// 가만히 선 주인공은 섬 밖이라 맞음 (몸 0 으로도)
			Assert.IsTrue(BandMonsterAttack.IsInBand(center, band, Vector3.forward, 360f, player, 0f), "가만히 서면 맞아야 함");
			// 섬 가운데는 안전, 바깥 끝 가까이도 맞음
			Assert.IsFalse(BandMonsterAttack.IsInBand(center, band, Vector3.forward, 360f, center, 0f));
			Assert.IsTrue(BandMonsterAttack.IsInBand(center, band, Vector3.forward, 360f, center + Vector3.right * (band.Outer - 0.1f), 0f));
			// 0 이면 주인공 자리 그대로 (지금 동작)
			Assert.AreEqual(player, BandMonsterAttack.OffsetCenter(player, 0f, 90f));
		}

		// ---- 2. 빈칸 벽

		[Test]
		public void Gap_wall_covers_the_player_and_opens_one_far_column()
		{
			ScatterZoneMonsterAttack wall = (ScatterZoneMonsterAttack)Dummy(972).Attack;
			Vector3 caster = new(0f, 0f, -5f);
			Vector3 player = Vector3.zero;
			List<Vector3> cells = new();
			ScatterZoneMonsterAttack.ArrangeAcross(caster, player, wall.Count, wall.Rows, wall.Distance, cells);
			Assert.AreEqual(wall.Count * wall.Rows, cells.Count);
			List<Vector3> safeSpots = new();
			ScatterZoneMonsterAttack.TakeGap(cells, 0, wall.Rows, player, wall.Gap, 0.99f, safeSpots);
			Assert.AreEqual(wall.Rows, safeSpots.Count, "빈칸은 세로 줄 하나 (줄 수만큼)");
			Assert.AreEqual((wall.Count - 1) * wall.Rows, cells.Count);

			// 가만히 선 주인공은 어느 원 안 (몸 0 으로도)
			Assert.IsTrue(cells.Any(cell => SlimeKingPatternMath.IsWithin(cell, player, wall.Radius)), "가만히 서면 맞아야 함");
			// 빈칸 밝은 원은 이웃 원 가장자리까지라 원 안 어디에 몸 중심이 있어도 이웃 원 밖
			Assert.Greater(wall.GapRadius, 0f);
			foreach (Vector3 spot in safeSpots)
			{
				float nearest = cells.Min(cell => Vector3.Distance(cell, spot));
				Assert.GreaterOrEqual(nearest, wall.Radius + wall.GapRadius - 1e-3f, "빈칸 밝은 원이 위험한 원과 겹침");
			}
			// 먼 쪽: 빈칸은 양 끝이 아닌 안쪽 세로 줄 (끝을 비우면 짧은 벽일 뿐)
			float gapDistance = Mathf.Abs(safeSpots[0].x);
			float endDistance = wall.Distance * (wall.Count - 1) * 0.5f;
			Assert.Less(gapDistance, endDistance - 1e-3f, "빈칸이 끝 줄");
			Assert.IsTrue(cells.Any(cell => Mathf.Abs(cell.x) > gapDistance + 1e-3f), "빈칸 바깥에도 덤불이 있어야 함");
			// 빈칸까지 옆걸음이 앞뒤로 빠져나가기보다 짧음 (빈칸이 정답). 초 단위 비교는 설계 README 와 연결 스크립트 주석
			float toGap = gapDistance - wall.GapRadius;
			float outOfRows = wall.Distance * (wall.Rows - 1) * 0.5f + wall.Radius;
			Assert.Less(toGap, outOfRows);
		}

		[Test]
		public void Gap_pick_is_random_or_farthest_with_ties_split()
		{
			List<Vector3> slots = new() { new(-3f, 0f, 0f), new(-1f, 0f, 0f), new(1f, 0f, 0f), new(3f, 0f, 0f) };
			// 먼 쪽은 양 끝을 빼고 고름. 가운데 둘이 같은 거리면 무작위
			Assert.AreEqual(1, ScatterZoneMonsterAttack.PickGap(slots, 0, Vector3.zero, ScatterGap.FarFromPlayer, 0f));
			Assert.AreEqual(2, ScatterZoneMonsterAttack.PickGap(slots, 0, Vector3.zero, ScatterGap.FarFromPlayer, 0.99f));
			Assert.AreEqual(2, ScatterZoneMonsterAttack.PickGap(slots, 0, new Vector3(-3f, 0f, 0f), ScatterGap.FarFromPlayer, 0f));
			Assert.AreEqual(2, ScatterZoneMonsterAttack.PickGap(slots, 0, Vector3.zero, ScatterGap.Random, 0.6f));
			Assert.AreEqual(3, ScatterZoneMonsterAttack.PickGap(slots, 0, Vector3.zero, ScatterGap.Random, 1f));
		}

		// ---- 3. 따라 도는 조준

		[Test]
		public void Tracking_turns_at_most_its_speed_and_locks_before_the_strike()
		{
			LineMonsterAttack aim = (LineMonsterAttack)Dummy(973).Attack;
			Vector3 turned = LineMonsterAttack.TurnToward(Vector3.forward, Vector3.right, aim.TrackDegreesPerSecond * 0.1f);
			Assert.AreEqual(aim.TrackDegreesPerSecond * 0.1f, Vector3.Angle(Vector3.forward, turned), 1e-3f);
			// 상한보다 가까우면 바로 맞춤
			Assert.AreEqual(0f, Vector3.Angle(Vector3.right, LineMonsterAttack.TurnToward(Vector3.forward, Vector3.right, 180f)), 1e-3f);
			// 고정 뒤 피할 틈이 남고, 고정 순간 다른 판으로 색이 바뀜
			float lockedSeconds = aim.WindupSeconds - aim.TrackSeconds;
			Assert.Greater(lockedSeconds, 0f);
			Assert.IsNotNull(aim.TrackingLockFx, "고정 순간 단서");
			Assert.AreNotSame(aim.WindupTelegraph, aim.TrackingLockFx, "고정 순간에 색이 바뀌어야 함");
		}

		// ---- 4. 엄폐

		[Test]
		public void Cover_blocks_only_obstacles_on_the_mask_between_caster_and_player()
		{
			GameObject caster = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			GameObject player = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
			try
			{
				caster.transform.position = Vector3.zero;
				player.transform.position = new Vector3(0f, 0f, 6f);
				rock.transform.position = new Vector3(0f, 0.5f, 3f);
				Physics.SyncTransforms();
				LayerMask defaultMask = 1;
				float height = ((BandMonsterAttack)Dummy(974).Attack).CoverHeight;
				Assert.IsTrue(MonsterCover.Blocks(caster.transform.position, player.transform.position, height, defaultMask, caster.transform, player.transform), "바위 뒤");
				Assert.IsFalse(MonsterCover.Blocks(caster.transform.position, player.transform.position, height, 0, caster.transform, player.transform), "층이 비면 엄폐 없음");
				// 옆으로 비키면 노출
				rock.transform.position = new Vector3(3f, 0.5f, 3f);
				Physics.SyncTransforms();
				Assert.IsFalse(MonsterCover.Blocks(caster.transform.position, player.transform.position, height, defaultMask, caster.transform, player.transform), "가리는 것 없음");
				// 시전자 몸에 붙은 콜라이더는 엄폐가 아님
				rock.transform.SetParent(caster.transform, false);
				rock.transform.localPosition = new Vector3(0f, 0.5f, 3f);
				Physics.SyncTransforms();
				Assert.IsFalse(MonsterCover.Blocks(caster.transform.position, player.transform.position, height, defaultMask, caster.transform, player.transform), "시전자 자신");
			}
			finally
			{
				Object.DestroyImmediate(caster);
				Object.DestroyImmediate(player);
				Object.DestroyImmediate(rock);
			}
		}

		// ---- 5. 시선

		[Test]
		public void Gaze_hits_only_when_walking_toward_the_caster()
		{
			GazeMonsterAttack gaze = (GazeMonsterAttack)Dummy(975).Attack;
			Vector3 caster = Vector3.zero;
			Vector3 viewer = new(0f, 0f, 4f);
			float half = gaze.GazeDegrees * 0.5f;
			Assert.IsTrue(GazeMonsterAttack.IsFacing(Vector3.back, viewer, caster, gaze.GazeDegrees), "보스 쪽으로 걸음");
			Assert.IsFalse(GazeMonsterAttack.IsFacing(Vector3.forward, viewer, caster, gaze.GazeDegrees), "등 돌림");
			Assert.IsTrue(GazeMonsterAttack.IsFacing(Quaternion.Euler(0f, half - 1f, 0f) * Vector3.back, viewer, caster, gaze.GazeDegrees), "각 안쪽 끝");
			Assert.IsFalse(GazeMonsterAttack.IsFacing(Quaternion.Euler(0f, half + 1f, 0f) * Vector3.back, viewer, caster, gaze.GazeDegrees), "각 바깥");
			Assert.IsFalse(GazeMonsterAttack.IsFacing(Vector3.zero, viewer, caster, gaze.GazeDegrees), "방향 없음은 안 본 것");
			Assert.IsTrue(gaze.HitEffect.AppliesStatus, "맞으면 상태이상");
			Assert.AreNotEqual(UnitStatusType.Fear, gaze.HitEffect.Status, "공포는 주인공 걸음에 안 걸림");
		}
	}
}
