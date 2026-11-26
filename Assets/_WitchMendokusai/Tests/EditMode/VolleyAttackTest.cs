using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 보스 탄막 부품 (VolleyMonsterAttack) 의 배치 계산과 탄막 더미 951~956. 정본 memo wm/design/boss-volley-20261126
	public class VolleyAttackTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/VolleyDummy";
		private const string ADDRESS_GROUP = "Assets/AddressableAssetsData/AssetGroups/Monster.asset";
		private readonly List<Object> created = new();

		// 시키는 행동. 설계 표 (memo README) 와 같은 말. 겹치면 같은 패턴으로 보임
		private static readonly Dictionary<int, string> asks = new()
		{
			{ 951, "고리의 틈을 찾아 지나고 두 번째 고리도" },
			{ 952, "부채 바깥으로 돌아 나가기" },
			{ 953, "나선을 따라 같은 쪽으로 돌다 꺾이면 다시 읽기" },
			{ 954, "갈래 사이 쐐기에 서기" },
			{ 955, "착탄 자리에서 분열 반경 밖으로" },
			{ 956, "사선으로 늘어선 탄 사이로 비집고 박힌 가시 피하기" },
		};

		[TearDown]
		public void TearDown()
		{
			foreach (Object asset in created)
				Object.DestroyImmediate(asset);
			created.Clear();
		}

		private static List<Monster> LoadDummies() =>
			AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER })
				.Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
				.OrderBy(monster => monster.ID)
				.ToList();

		private static VolleyMonsterAttack AttackOf(Monster monster) => (VolleyMonsterAttack)monster.Attack;

		[Test]
		public void Ring_spreads_evenly_and_the_next_wave_turns()
		{
			List<VolleyShot> shots = new();
			VolleyPatternMath.Wave(VolleyShape.Ring, 8, 0f, 1, 0f, 0, 22.5f, 4f, 0f, shots);
			Assert.AreEqual(8, shots.Count);
			for (int i = 0; i < shots.Count; i++)
				Assert.AreEqual(Mathf.DeltaAngle(0f, 45f * i), Mathf.DeltaAngle(0f, VolleyPatternMath.AngleOf(shots[i].Direction)), 0.01f, $"고리 {i}번");
			float first = VolleyPatternMath.AngleOf(shots[0].Direction);
			VolleyPatternMath.Wave(VolleyShape.Spiral, 8, 0f, 1, 0f, 1, 22.5f, 4f, 0f, shots);
			Assert.AreEqual(22.5f, Mathf.DeltaAngle(first, VolleyPatternMath.AngleOf(shots[0].Direction)), 0.01f, "둘째 물결은 돌아감");
			VolleyPatternMath.Wave(VolleyShape.Spiral, 8, 0f, 1, 0f, 1, -22.5f, 4f, 0f, shots);
			Assert.AreEqual(-22.5f, Mathf.DeltaAngle(first, VolleyPatternMath.AngleOf(shots[0].Direction)), 0.01f, "부호가 회전 방향");
		}

		[Test]
		public void Double_ring_second_wave_fills_between_the_first()
		{
			VolleyMonsterAttack ring = AttackOf(LoadDummies().Single(monster => monster.ID == 951));
			Assert.AreEqual(VolleyShape.Ring, ring.Shape);
			Assert.Greater(ring.Waves, 1);
			// 둘째 고리는 반 칸 엇갈려 첫 고리의 틈에서 옴. 같은 자리에 서 있으면 두 번째에 맞음
			Assert.AreEqual(VolleyPatternMath.NeighborDegrees(ring.Shape, ring.Count, ring.ArcDegrees) * 0.5f, Mathf.Abs(ring.TurnDegreesPerWave), 0.01f);
			Assert.Greater(ring.WaveInterval, 0f, "두 고리 사이 박자");
		}

		[Test]
		public void Segment_hit_does_not_tunnel_through_the_player()
		{
			// 한 프레임에 2 m 지나간 탄도 길 가운데 선 주인공에게 닿음
			Assert.AreEqual(0.1f, VolleyPatternMath.SegmentDistance(new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 5f, 0.1f)), 0.001f);
			Assert.AreEqual(1f, VolleyPatternMath.SegmentDistance(new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)), 0.001f);
		}

		[Test]
		public void Spiral_reverses_after_the_given_wave()
		{
			// 셋째 물결까지 10도씩 가고 그 뒤로 되돌아옴
			Assert.AreEqual(30f, VolleyPatternMath.TurnOffset(3, 10f, 3), 0.001f);
			Assert.AreEqual(20f, VolleyPatternMath.TurnOffset(4, 10f, 3), 0.001f);
			Assert.AreEqual(0f, VolleyPatternMath.TurnOffset(6, 10f, 3), 0.001f);
			Assert.AreEqual(60f, VolleyPatternMath.TurnOffset(6, 10f, 0), 0.001f, "0 이면 안 꺾음");
			VolleyMonsterAttack spiral = AttackOf(LoadDummies().Single(monster => monster.ID == 953));
			Assert.Greater(spiral.ReverseAtWave, 0, "953 은 도중에 꺾임");
			Assert.Less(spiral.ReverseAtWave, spiral.Waves - 1);
		}

		[Test]
		public void Fan_is_centered_on_the_aim_and_later_shots_are_slower()
		{
			List<VolleyShot> shots = new();
			VolleyPatternMath.Wave(VolleyShape.Fan, 6, 50f, 1, 90f, 0, 0f, 6f, 0.7f, shots);
			Assert.AreEqual(6, shots.Count);
			Assert.AreEqual(65f, VolleyPatternMath.AngleOf(shots[0].Direction), 0.01f);
			Assert.AreEqual(115f, VolleyPatternMath.AngleOf(shots[5].Direction), 0.01f);
			for (int i = 1; i < shots.Count; i++)
				Assert.Less(shots[i].Speed, shots[i - 1].Speed, "뒤 탄일수록 느림");
			Assert.AreEqual(10f, VolleyPatternMath.NeighborDegrees(VolleyShape.Fan, 6, 50f), 0.001f);
		}

		[Test]
		public void Spokes_make_lines_of_shots_with_different_speeds()
		{
			List<VolleyShot> shots = new();
			VolleyPatternMath.Wave(VolleyShape.Spokes, 8, 0f, 4, 0f, 0, 0f, 5f, 0.8f, shots);
			Assert.AreEqual(VolleyPatternMath.ShotCount(VolleyShape.Spokes, 8, 4), shots.Count);
			// 한 갈래의 넷은 같은 쪽, 빠르기만 다름
			for (int j = 1; j < 4; j++)
			{
				Assert.AreEqual(0f, Vector3.Angle(shots[0].Direction, shots[j].Direction), 0.01f);
				Assert.Less(shots[j].Speed, shots[j - 1].Speed);
			}
			Assert.AreEqual(45f, Vector3.Angle(shots[0].Direction, shots[4].Direction), 0.01f, "갈래 사이 45도");
		}

		[Test]
		public void Every_volley_dummy_exists_with_an_address()
		{
			List<Monster> dummies = LoadDummies();
			CollectionAssert.AreEquivalent(asks.Keys, dummies.Select(monster => monster.ID));
			string group = File.ReadAllText(ADDRESS_GROUP).Replace("\r\n", "\n");
			foreach (Monster monster in dummies)
			{
				string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(monster));
				StringAssert.Contains($"m_GUID: {guid}\n    m_Address: Monster/{monster.ID}\n", group, monster.name);
				Assert.IsEmpty(monster.Loots, $"{monster.name} 더미는 드랍 없음");
			}
		}

		[Test]
		public void Volley_attacks_pass_validation_and_fill_the_five_slots()
		{
			foreach (Monster monster in LoadDummies())
			{
				Assert.IsInstanceOf<VolleyMonsterAttack>(monster.Attack, monster.name);
				List<string> errors = new();
				monster.Attack.Validate(monster, errors);
				Assert.IsEmpty(errors, string.Join("\n", errors));
				VolleyMonsterAttack volley = AttackOf(monster);
				// 몸동작, 예고 물체, 타격. 남는 것은 박히는 두 더미 (955, 956)
				Assert.IsFalse(volley.Pose.IsEmpty, $"{volley.name} 몸동작 없음");
				Assert.IsNotNull(volley.WindupFx, $"{volley.name} 예고 물체 없음");
				Assert.IsNotNull(volley.StrikeFx, $"{volley.name} 타격 이펙트 없음");
				Assert.IsNotNull(volley.WindupTelegraph, $"{volley.name} 바닥 예고 없음");
				Assert.IsNotNull(volley.PopFx, $"{volley.name} 탄이 맞거나 박힐 때 이펙트 없음");
			}
			Assert.AreEqual(2, LoadDummies().Count(monster => AttackOf(monster).Planted.IsDangerous), "박혀 남는 탄 더미는 둘");
		}

		[Test]
		public void Shapes_and_asks_do_not_repeat()
		{
			HashSet<string> shapes = new();
			HashSet<string> seenAsks = new();
			foreach (Monster monster in LoadDummies())
			{
				VolleyMonsterAttack volley = AttackOf(monster);
				string shape = $"{volley.Shape}/{(volley.SpeedStep > 0f ? "느려짐" : "고름")}/{(volley.Planted.Seconds > 0f ? "박힘" : "사라짐")}";
				Assert.IsTrue(shapes.Add(shape), $"{monster.name} 짜임 {shape} 이 다른 더미와 겹침");
				Assert.IsTrue(seenAsks.Add(asks[monster.ID]), $"{monster.name} 시키는 행동이 겹침");
			}
		}

		[Test]
		public void Gaps_are_wider_than_the_player_body()
		{
			// 주인공 몸 반지름 0.3 m 의 두 배, 걸음 2.9 m/s (EliteBossAttackTest 와 같은 값)
			foreach (Monster monster in LoadDummies())
			{
				VolleyMonsterAttack volley = AttackOf(monster);
				float neighbor = VolleyPatternMath.NeighborDegrees(volley.Shape, volley.Count, volley.ArcDegrees);
				if (volley.Shape == VolleyShape.Split)
				{
					// 분열은 큰 탄이 날아오는 예고 동안 반경 밖으로 걸어 나갈 수 있어야 함. 앉은 자리 고리에도 틈
					Assert.GreaterOrEqual(volley.WindupSeconds * 2.9f, volley.Range + 0.3f, $"{volley.name} 분열 반경 밖으로 못 나감");
					Assert.Greater(VolleyPatternMath.GapAt(neighbor, volley.Range, volley.BulletRadius), 0.3f * 2f, $"{volley.name} 고리에 틈이 없음");
					continue;
				}
				// 준비를 시작한 거리 (사거리 끝) 에서 이웃 탄 사이로 몸이 지나감. 더 가까우면 좁아지므로 물러나서 지나는 것이 답 (README)
				Assert.Greater(VolleyPatternMath.GapAt(neighbor, volley.TriggerRange, volley.BulletRadius), 0.3f * 2f, $"{volley.name} 틈이 몸보다 좁음");
				Assert.GreaterOrEqual(volley.Range, volley.TriggerRange, $"{volley.name} 탄이 사거리 끝까지 못 감");
			}
		}

		[Test]
		public void Spiral_turns_no_faster_than_the_player_can_follow_inside_trigger_range()
		{
			VolleyMonsterAttack spiral = AttackOf(LoadDummies().Single(monster => monster.ID == 953));
			Assert.AreEqual(VolleyShape.Spiral, spiral.Shape);
			// 갈래 끝이 도는 빠르기 (m/s) 가 사거리 안 어딘가에서 걸음 2.9 m/s 이하
			float radiansPerSecond = Mathf.Abs(spiral.TurnDegreesPerWave) / spiral.WaveInterval * Mathf.Deg2Rad;
			Assert.Less(2.9f / radiansPerSecond, spiral.TriggerRange, "같이 돌 수 있는 반지름이 사거리 밖");
		}

		[Test]
		public void Validation_catches_a_spiral_that_does_not_turn_and_a_target_linger()
		{
			Monster owner = LoadDummies().Single(monster => monster.ID == 953);
			VolleyMonsterAttack copy = Object.Instantiate(AttackOf(owner));
			created.Add(copy);
			SerializedObject serialized = new(copy);
			serialized.FindProperty("<TurnDegreesPerWave>k__BackingField").floatValue = 0f;
			serialized.FindProperty("<Linger>k__BackingField").FindPropertyRelative("<Seconds>k__BackingField").floatValue = 1f;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			List<string> errors = new();
			copy.Validate(owner, errors);
			Assert.IsTrue(errors.Exists(error => error.Contains("나선")), string.Join("\n", errors));
			Assert.IsTrue(errors.Exists(error => error.Contains(nameof(VolleyMonsterAttack.Planted))), string.Join("\n", errors));
		}
	}
}
