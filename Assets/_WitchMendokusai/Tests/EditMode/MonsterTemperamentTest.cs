using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 몬스터 걷는 성격 (MonsterTemperament) 열셋의 결정과 더미 몬스터 연결. 정본 memo wm/design/monster-temperament-20261126
	// 성격은 위치와 시간만 받는 순수 계산이라 장면 없이 돌림. 수치는 자산 기본값
	public class MonsterTemperamentTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/TemperamentDummy";
		private const int FIRST_DUMMY_ID = 901;
		private const int DUMMY_COUNT = 13;
		private readonly List<Object> created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object instance in created)
				Object.DestroyImmediate(instance);
			created.Clear();
		}

		private T Make<T>() where T : MonsterTemperament
		{
			T temperament = ScriptableObject.CreateInstance<T>();
			created.Add(temperament);
			return temperament;
		}

		private static TemperamentMemory Fresh(float seed = 0.25f)
		{
			TemperamentMemory memory = new();
			memory.Reset(Vector3.zero, seed);
			return memory;
		}

		// 주인공은 (0,0,0), 나는 x 축 위. 따로 주지 않으면 주인공은 멈춰 +x 를 봄
		private static TemperamentContext At(float selfX, float now = 0f, float selfZ = 0f) => new()
		{
			Self = new Vector3(selfX, 0f, selfZ),
			Player = Vector3.zero,
			PlayerVelocity = Vector3.zero,
			PlayerLook = Vector3.right,
			Now = now,
			HealthRatio = 1f,
			SinceHit = float.PositiveInfinity,
		};

		private static float TowardPlayer(TemperamentStep step, in TemperamentContext context) => Vector3.Dot(step.Direction.normalized * step.Speed, context.ToPlayer.normalized);

		[Test]
		public void Ambush_aims_ahead_of_moving_player()
		{
			AmbushTemperament ambush = Make<AmbushTemperament>();
			TemperamentContext context = At(-10f);
			context.PlayerVelocity = new Vector3(0f, 0f, 4f);
			TemperamentStep step = ambush.Steer(context, Fresh());
			Assert.Greater(step.Direction.z, 0.2f, "주인공이 +z 로 가면 그 앞 (+z) 으로 꺾어야 함");
		}

		[Test]
		public void Flank_goes_around_when_in_front_and_closes_from_behind()
		{
			FlankTemperament flank = Make<FlankTemperament>();
			// 주인공이 +x 를 보고, 나는 그 앞 +x 쪽
			TemperamentStep front = flank.Steer(At(6f), Fresh());
			Assert.Greater(Mathf.Abs(front.Direction.z), 0.3f, "정면이면 옆으로 돌아감");
			// 등 뒤 자리 (-2.5, 0) 근처
			TemperamentContext behind = At(-2.6f);
			TemperamentStep strike = flank.Steer(behind, Fresh());
			Assert.Greater(TowardPlayer(strike, behind), 0.9f, "등 뒤 자리에 닿으면 곧장 덮침");
		}

		[Test]
		public void Shy_retreats_when_close_and_returns_only_past_return_radius()
		{
			ShyTemperament shy = Make<ShyTemperament>();
			TemperamentMemory memory = Fresh();
			Assert.Greater(TowardPlayer(shy.Steer(At(10f), memory), At(10f)), 0.9f, "멀면 다가옴");
			Assert.Less(TowardPlayer(shy.Steer(At(3f), memory), At(3f)), -0.5f, "가까우면 물러남");
			Assert.Less(TowardPlayer(shy.Steer(At(5.5f), memory), At(5.5f)), -0.5f, "물러나는 중엔 돌아올 거리 전까지 계속 물러남");
			Assert.Greater(TowardPlayer(shy.Steer(At(8f), memory), At(8f)), 0.9f, "돌아올 거리 밖이면 다시 다가옴");
		}

		[Test]
		public void Kite_backs_off_near_strafes_in_band_and_approaches_far()
		{
			KiteTemperament kite = Make<KiteTemperament>();
			TemperamentMemory memory = Fresh();
			Assert.Less(TowardPlayer(kite.Steer(At(2f), memory), At(2f)), -0.5f);
			Assert.Greater(TowardPlayer(kite.Steer(At(12f), memory), At(12f)), 0.9f);
			TemperamentStep strafe = kite.Steer(At(5.5f), memory);
			Assert.Less(Mathf.Abs(strafe.Direction.x), 0.1f, "띠 안에서는 옆걸음 (주인공 쪽 성분 없음)");
			Assert.IsTrue(strafe.FacesPlayer);
		}

		[Test]
		public void Orbit_moves_mostly_sideways_and_corrects_radius()
		{
			OrbitTemperament orbit = Make<OrbitTemperament>();
			TemperamentMemory memory = Fresh();
			TemperamentStep onCircle = orbit.Steer(At(6f, 0f), memory);
			Assert.Less(Mathf.Abs(onCircle.Direction.x), 0.1f, "원 위면 접선으로");
			TemperamentContext far = At(15f, 0.1f);
			Assert.Greater(TowardPlayer(orbit.Steer(far, memory), far), 0.3f, "원보다 멀면 안으로 끌림");
		}

		[Test]
		public void Hop_rests_then_bursts_toward_player()
		{
			HopTemperament hop = Make<HopTemperament>();
			TemperamentMemory memory = Fresh();
			TemperamentStep first = hop.Steer(At(8f, 0f), memory);
			Assert.Greater(first.BurstSeconds, 0f, "처음 차례는 뜀");
			Assert.Less(first.BurstVelocity.x, 0f, "주인공 쪽 (-x) 으로");
			Assert.Greater(first.Punch, 1f, "뛰기 전 웅크림");
			TemperamentStep landed = hop.Steer(At(5f, 0.5f), memory);
			Assert.AreEqual(0f, landed.Speed, "뛴 뒤에는 멈춤");
			TemperamentStep resting = hop.Steer(At(5f, 0.6f), memory);
			Assert.AreEqual(0f, resting.BurstSeconds, "쉬는 동안 다시 안 뜀");
		}

		[Test]
		public void Zigzag_swings_both_sides_over_a_period()
		{
			ZigzagTemperament zigzag = Make<ZigzagTemperament>();
			TemperamentMemory memory = Fresh(0f);
			float minZ = float.MaxValue;
			float maxZ = float.MinValue;
			for (int i = 0; i < 14; i++)
			{
				TemperamentStep step = zigzag.Steer(At(10f, i * 0.1f), memory);
				minZ = Mathf.Min(minZ, step.Direction.z);
				maxZ = Mathf.Max(maxZ, step.Direction.z);
				Assert.Less(step.Direction.x, 0f, "꺾여도 주인공 쪽으로 감");
			}
			Assert.Less(minZ, -0.5f);
			Assert.Greater(maxZ, 0.5f);
		}

		[Test]
		public void Passive_ignores_player_until_hit_then_chases_then_calms()
		{
			PassiveTemperament passive = Make<PassiveTemperament>();
			TemperamentMemory memory = Fresh();
			memory.Reset(new Vector3(10f, 0f, 0f), 0.25f);
			TemperamentStep calm = passive.Steer(At(10f, 0f), memory);
			Assert.LessOrEqual(calm.Speed, 0.5f, "모른 척 천천히 거닒");
			TemperamentContext hit = At(10f, 1f);
			hit.SinceHit = 0f;
			Assert.Greater(passive.Steer(hit, memory).Punch, 1f, "맞은 순간 화남 (부풀림)");
			hit.SinceHit = 0.5f;
			Assert.Greater(TowardPlayer(passive.Steer(hit, memory), hit), 0.9f, "화나면 쫓아옴");
			TemperamentContext later = At(10f, 20f);
			later.SinceHit = 20f;
			Assert.LessOrEqual(passive.Steer(later, memory).Speed, 0.5f, "풀리면 다시 거닒");
		}

		[Test]
		public void Sleeper_wakes_by_distance_startles_then_pounces()
		{
			SleeperTemperament sleeper = Make<SleeperTemperament>();
			TemperamentMemory memory = Fresh();
			Assert.AreEqual(0f, sleeper.Steer(At(8f, 0f), memory).Speed, "멀면 잠");
			Assert.IsTrue(SleeperTemperament.IsAsleep(memory));
			TemperamentStep startle = sleeper.Steer(At(2f, 1f), memory);
			Assert.Greater(startle.Punch, 1f, "가까이 오면 놀람");
			Assert.AreEqual(0f, sleeper.Steer(At(2f, 1.2f), memory).Speed, "놀란 동안은 굳음");
			TemperamentStep pounce = sleeper.Steer(At(2f, 2f), memory);
			Assert.Greater(pounce.BurstSeconds, 0f, "놀람이 끝나면 덮침");
			Assert.Less(pounce.BurstVelocity.x, 0f);
		}

		[Test]
		public void Sleeper_wakes_when_hit_from_afar_and_sleeps_again_when_left_alone()
		{
			SleeperTemperament sleeper = Make<SleeperTemperament>();
			TemperamentMemory memory = Fresh();
			TemperamentContext hit = At(9f, 0f);
			hit.SinceHit = 0f;
			sleeper.Steer(hit, memory);
			Assert.IsFalse(SleeperTemperament.IsAsleep(memory), "맞으면 멀어도 깸");
			sleeper.Steer(At(9f, 1f), memory);
			sleeper.Steer(At(20f, 2f), memory);
			sleeper.Steer(At(20f, 7f), memory);
			Assert.IsTrue(SleeperTemperament.IsAsleep(memory), "오래 멀리 있으면 다시 잠");
		}

		[Test]
		public void Coward_flees_after_hit_and_when_low_health()
		{
			CowardTemperament coward = Make<CowardTemperament>();
			TemperamentContext context = At(5f);
			Assert.Greater(TowardPlayer(coward.Steer(context, Fresh()), context), 0.9f);
			context.SinceHit = 0.1f;
			Assert.Less(TowardPlayer(coward.Steer(context, Fresh()), context), -0.9f, "맞으면 움찔해 달아남");
			context.SinceHit = 10f;
			context.HealthRatio = 0.2f;
			Assert.Less(TowardPlayer(coward.Steer(context, Fresh()), context), -0.9f, "체력이 적으면 계속 달아남");
		}

		[Test]
		public void Bashful_freezes_when_watched_and_sneaks_when_not()
		{
			BashfulTemperament bashful = Make<BashfulTemperament>();
			// 주인공은 +x 를 봄
			Assert.AreEqual(0f, bashful.Steer(At(5f), Fresh()).Speed, "앞에 있으면 얼어붙음");
			TemperamentContext behind = At(-5f);
			Assert.Greater(TowardPlayer(bashful.Steer(behind, Fresh()), behind), 0.9f, "등 뒤면 다가옴");
		}

		[Test]
		public void Mirror_reflects_player_movement_across_the_gap()
		{
			MirrorTemperament mirror = Make<MirrorTemperament>();
			TemperamentContext context = At(5f);
			context.PlayerVelocity = new Vector3(3f, 0f, 0f);
			Assert.Less(mirror.Steer(context, Fresh()).Direction.x, -0.9f, "다가가면 마주 옴");
			context.PlayerVelocity = new Vector3(0f, 0f, 3f);
			Assert.Greater(mirror.Steer(context, Fresh()).Direction.z, 0.9f, "옆으로 가면 같은 옆으로");
			context.PlayerVelocity = Vector3.zero;
			Assert.AreEqual(0f, mirror.Steer(context, Fresh()).Speed, "멈추면 같이 멈춤");
		}

		[Test]
		public void Tide_switches_together_between_chase_and_scatter()
		{
			TideTemperament tide = Make<TideTemperament>();
			Assert.IsTrue(tide.IsChasing(1f));
			Assert.IsFalse(tide.IsChasing(8f));
			TemperamentMemory first = Fresh(0f);
			TemperamentMemory second = Fresh(0.5f);
			Assert.Greater(TowardPlayer(tide.Steer(At(5f, 1f), first), At(5f, 1f)), 0.9f);
			TemperamentStep turn = tide.Steer(At(5f, 8f), first);
			Assert.Greater(turn.Punch, 1f, "물결이 바뀌는 순간 몸짓");
			Assert.Less(TowardPlayer(tide.Steer(At(5f, 8.1f), second), At(5f, 8.1f)), 0f, "썰물에는 구석 (멀리) 으로");
		}

		[Test]
		public void Dummy_monsters_each_have_a_distinct_temperament()
		{
			HashSet<System.Type> kinds = new();
			int found = 0;
			foreach (string guid in AssetDatabase.FindAssets("t:Monster", new[] { MONSTER_FOLDER }))
			{
				Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid));
				Assert.That(monster.ID, Is.InRange(FIRST_DUMMY_ID, FIRST_DUMMY_ID + DUMMY_COUNT - 1), monster.name);
				Assert.IsNotNull(monster.Temperament, $"{monster.name} 성격 없음");
				Assert.IsTrue(kinds.Add(monster.Temperament.GetType()), $"{monster.name} 성격이 다른 더미와 겹침");
				found++;
			}
			Assert.AreEqual(DUMMY_COUNT, found);
		}
	}
}
