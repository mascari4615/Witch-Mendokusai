using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 몬스터 걷는 성격 (MonsterTemperament) 스물 (1묶음 13, 2묶음 7) 의 결정과 더미 몬스터 연결. 정본 memo wm/design/monster-temperament-20261126
	// 성격은 위치와 시간만 받는 순수 계산이라 장면 없이 돌림. 수치는 자산 기본값
	public class MonsterTemperamentTest
	{
		private const string MONSTER_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/TemperamentDummy";
		private const int FIRST_DUMMY_ID = 901;
		private const int DUMMY_COUNT = 20;
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
			SinceFallen = float.PositiveInfinity,
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
		public void Hop_waits_then_crouches_then_bursts_toward_player()
		{
			HopTemperament hop = Make<HopTemperament>();
			TemperamentMemory memory = Fresh();
			TemperamentStep first = hop.Steer(At(8f, 0f), memory);
			Assert.AreEqual(0f, first.BurstSeconds, "막 태어나면 바로 뛰지 않고 유닛마다 다른 쉼부터");
			TemperamentStep crouch = hop.Steer(At(8f, 5f), memory);
			Assert.IsTrue(HopTemperament.IsCrouching(memory), "쉼 뒤에는 웅크림");
			Assert.Greater(crouch.Punch, 1f, "웅크릴 때 부풂");
			Assert.AreEqual(0f, crouch.Speed, "웅크린 동안 멈춤");
			Assert.IsTrue(crouch.HoldsAttack);
			TemperamentStep jump = hop.Steer(At(8f, 6f), memory);
			Assert.Greater(jump.BurstSeconds, 0f, "웅크림 뒤 뜀");
			Assert.Less(jump.BurstVelocity.x, 0f, "주인공 쪽 (-x) 으로");
			TemperamentStep landed = hop.Steer(At(5f, 7f), memory);
			Assert.AreEqual(0f, landed.Speed, "뛴 뒤 멈춤");
			Assert.AreEqual(0f, hop.Steer(At(5f, 7.1f), memory).BurstSeconds, "쉬는 동안 다시 안 뜀");
		}

		[Test]
		public void Hop_first_rest_differs_by_seed()
		{
			HopTemperament hop = Make<HopTemperament>();
			TemperamentMemory early = Fresh(0f);
			TemperamentMemory late = Fresh(1f);
			hop.Steer(At(8f, 0f), early);
			hop.Steer(At(8f, 0f), late);
			Assert.Less(early.NextTime, late.NextTime, "같은 물결이 한꺼번에 첫 뜀을 하지 않음");
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
			Assert.Greater(calm.Speed, 0f, "거닒");
			Assert.LessOrEqual(calm.Speed, 0.5f, "천천히");
			Assert.IsTrue(calm.HoldsAttack, "모른 척하는 동안 공격 안 함");
			TemperamentContext hit = At(10f, 1f);
			hit.SinceHit = 0f;
			Assert.Greater(passive.Steer(hit, memory).Punch, 1f, "맞은 순간 화남 (부풀림)");
			hit.SinceHit = 0.5f;
			TemperamentStep angry = passive.Steer(hit, memory);
			Assert.Greater(TowardPlayer(angry, hit), 0.9f, "화나면 쫓아옴");
			Assert.IsFalse(angry.HoldsAttack, "화나면 공격도 함");
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
			Assert.IsTrue(sleeper.Steer(At(8f, 0.1f), memory).HoldsAttack, "자는 동안 공격 안 함");
			Assert.IsFalse(sleeper.Steer(At(8f, 0.2f), memory).FacesPlayer, "자면서 주인공 쪽으로 안 돎");
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
			TemperamentStep flinch = coward.Steer(context, Fresh());
			Assert.Less(TowardPlayer(flinch, context), -0.9f, "맞으면 움찔해 달아남");
			Assert.IsTrue(flinch.HoldsAttack, "달아나는 동안 공격 안 함");
			context.SinceHit = 10f;
			context.HealthRatio = 0.2f;
			Assert.Less(TowardPlayer(coward.Steer(context, Fresh()), context), -0.9f, "체력이 적으면 계속 달아남");
		}

		[Test]
		public void Bashful_freezes_when_watched_and_sneaks_when_not()
		{
			BashfulTemperament bashful = Make<BashfulTemperament>();
			// 주인공은 +x 를 봄
			TemperamentStep frozen = bashful.Steer(At(5f), Fresh());
			Assert.AreEqual(0f, frozen.Speed, "앞에 있으면 얼어붙음");
			Assert.IsTrue(frozen.HoldsAttack);
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
			TemperamentMemory memory = Fresh();
			Assert.Greater(TowardPlayer(tide.Steer(At(5f, 1f), memory), At(5f, 1f)), 0.9f);
			Assert.Greater(tide.Steer(At(5f, 8f), memory).Punch, 1f, "물결이 바뀌는 순간 몸짓");
		}

		// 구석을 썰물 시작에 정해 두므로, 비껴 흩어지는 유닛 (Seed 0 이면 60도) 도 주인공 둘레를 돌지 않고 멀어짐
		[TestCase(0f)]
		[TestCase(0.5f)]
		[TestCase(1f)]
		public void Tide_scatter_moves_away_and_reaches_corner(float seed)
		{
			TideTemperament tide = Make<TideTemperament>();
			TemperamentMemory memory = Fresh(seed);
			tide.Steer(At(5f, 1f), memory);
			Vector3 self = new(5f, 0f, 0f);
			float startDistance = self.magnitude;
			for (int i = 0; i < 40; i++)
			{
				TemperamentContext context = At(self.x, 7.05f + i * 0.1f, self.z);
				TemperamentStep step = tide.Steer(context, memory);
				self += step.Direction.normalized * step.Speed * 0.4f;
			}
			Assert.Greater(self.magnitude, startDistance + 2.5f, "흩어지면 주인공에게서 멀어짐");
			Assert.Less(Vector3.Distance(self, memory.Anchor), 1f, "정한 구석에 닿음");
		}

		// --- 2묶음 ---

		[Test]
		public void Blink_winds_up_then_teleports_behind_player_and_freezes()
		{
			BlinkTemperament blink = Make<BlinkTemperament>();
			TemperamentMemory memory = Fresh(0f);
			// Seed 0 이면 첫 점멸은 쿨다운의 절반 (2.5초) 뒤
			TemperamentContext early = At(6f, 0f);
			Assert.Greater(TowardPlayer(blink.Steer(early, memory), early), 0.9f, "때가 되기 전엔 쫓음");
			TemperamentStep windup = blink.Steer(At(6f, 3f), memory);
			Assert.AreEqual(0f, windup.Speed, "예고에는 멈춤");
			Assert.Greater(windup.Punch, 1f, "예고에 몸 부풀림");
			Assert.IsTrue(windup.HoldsAttack);
			Assert.IsFalse(windup.Teleport);
			Assert.IsTrue(BlinkTemperament.IsWindingUp(memory));
			TemperamentStep jump = blink.Steer(At(6f, 3.7f), memory);
			Assert.IsTrue(jump.Teleport, "예고가 끝나면 순간이동");
			// 주인공은 +x 를 봄. 등 뒤는 -x
			Assert.Less(jump.TeleportTo.x, -2f, "등 뒤로");
			Assert.IsTrue(blink.Steer(At(-2.5f, 3.9f), memory).HoldsAttack, "나타난 뒤 굳음 (빈틈)");
			TemperamentContext after = At(-2.5f, 4.3f);
			Assert.Greater(TowardPlayer(blink.Steer(after, memory), after), 0.9f, "굳음이 끝나면 다시 쫓음");
		}

		[Test]
		public void Blink_does_not_teleport_when_too_close_or_far()
		{
			BlinkTemperament blink = Make<BlinkTemperament>();
			TemperamentMemory memory = Fresh(0f);
			blink.Steer(At(1f, 0f), memory);
			Assert.IsFalse(blink.Steer(At(1f, 10f), memory).HoldsAttack, "너무 가까우면 점멸 안 하고 쫓음");
			Assert.IsFalse(blink.Steer(At(30f, 10f), memory).HoldsAttack, "너무 멀어도 점멸 안 함");
		}

		[Test]
		public void Flock_is_timid_alone_and_charges_in_a_group()
		{
			FlockTemperament flock = Make<FlockTemperament>();
			TemperamentContext alone = At(3f);
			Assert.Less(TowardPlayer(flock.Steer(alone, Fresh()), alone), -0.5f, "혼자서 가까우면 물러남");
			TemperamentContext lonelyFar = At(10f);
			lonelyFar.KinCount = 1;
			lonelyFar.KinCenter = new Vector3(10f, 0f, 6f);
			Assert.Greater(flock.Steer(lonelyFar, Fresh()).Direction.z, 0.9f, "모자라면 무리 쪽으로");
			TemperamentContext group = At(3f);
			group.KinCount = 3;
			group.KinCenter = new Vector3(3.5f, 0f, 0f);
			TemperamentMemory memory = Fresh();
			TemperamentStep charge = flock.Steer(group, memory);
			Assert.Greater(TowardPlayer(charge, group), 0.9f, "무리면 덤빔");
			Assert.Greater(charge.Punch, 1f, "용감해지는 순간 부풀림");
			Assert.AreEqual(0f, flock.Steer(group, memory).Punch, "부풀림은 한 번");
		}

		[Test]
		public void Guard_stands_between_and_intercepts_near_ally()
		{
			GuardTemperament guard = Make<GuardTemperament>();
			TemperamentContext none = At(5f);
			Assert.Greater(TowardPlayer(guard.Steer(none, Fresh()), none), 0.9f, "지킬 몬스터가 없으면 쫓음");
			// 지킬 몬스터는 (10,0,0). 지킬 자리는 그 앞 주인공 쪽 (8.2,0,0). 나는 옆 (10,0,3)
			TemperamentContext posted = At(10f, 0f, 3f);
			posted.HasAlly = true;
			posted.Ally = new Vector3(10f, 0f, 0f);
			TemperamentStep toPost = guard.Steer(posted, Fresh());
			Assert.Less(toPost.Direction.z, -0.5f, "지킬 자리로 감");
			Assert.Less(toPost.Direction.x, 0f, "지킬 몬스터보다 주인공 쪽");
			TemperamentContext threatened = posted;
			threatened.Ally = new Vector3(2f, 0f, 0f);
			TemperamentMemory memory = Fresh();
			TemperamentStep intercept = guard.Steer(threatened, memory);
			Assert.Greater(TowardPlayer(intercept, threatened), 0.9f, "주인공이 다가오면 막아섬");
			Assert.Greater(intercept.Punch, 1f);
		}

		[Test]
		public void Lurk_hides_behind_ally_and_pops_out_when_close()
		{
			LurkTemperament lurk = Make<LurkTemperament>();
			TemperamentContext hiding = At(6f, 0f, 2f);
			hiding.HasAlly = true;
			hiding.Ally = new Vector3(5f, 0f, 0f);
			TemperamentMemory memory = Fresh();
			TemperamentStep hide = lurk.Steer(hiding, memory);
			Assert.IsTrue(hide.HoldsAttack, "숨은 동안 공격 안 함");
			Assert.Greater(hide.Direction.x, 0f, "가림막 뒤 (주인공 반대쪽) 로");
			Assert.Less(hide.Direction.z, 0f);
			TemperamentContext close = At(2.5f);
			close.HasAlly = true;
			close.Ally = new Vector3(5f, 0f, 0f);
			TemperamentStep pop = lurk.Steer(close, memory);
			Assert.Greater(pop.BurstSeconds, 0f, "가까이 오면 튀어나옴");
			Assert.IsTrue(LurkTemperament.IsOut(memory));
			TemperamentContext far = At(10f);
			far.HasAlly = true;
			far.Ally = new Vector3(5f, 0f, 0f);
			lurk.Steer(far, memory);
			Assert.IsFalse(LurkTemperament.IsOut(memory), "멀어지면 다시 숨음");
		}

		[Test]
		public void Greedy_sits_on_loot_and_gets_angry_when_approached()
		{
			GreedyTemperament greedy = Make<GreedyTemperament>();
			TemperamentContext noLoot = At(5f);
			Assert.Greater(TowardPlayer(greedy.Steer(noLoot, Fresh()), noLoot), 0.9f, "전리품이 없으면 쫓음");
			TemperamentContext loot = At(5f);
			loot.HasLoot = true;
			loot.Loot = new Vector3(5f, 0f, 4f);
			Assert.Greater(greedy.Steer(loot, Fresh()).Direction.z, 0.9f, "전리품 쪽으로");
			TemperamentContext sitting = At(5f, 0f, 4f);
			sitting.HasLoot = true;
			sitting.Loot = new Vector3(5f, 0f, 4.2f);
			TemperamentMemory memory = Fresh();
			TemperamentStep sit = greedy.Steer(sitting, memory);
			Assert.AreEqual(0f, sit.Speed, "전리품 위에 앉음");
			Assert.IsTrue(sit.HoldsAttack, "먹느라 공격 안 함");
			TemperamentContext poked = sitting;
			poked.SinceHit = 0f;
			poked.Now = 1f;
			Assert.Greater(greedy.Steer(poked, memory).Punch, 1f, "맞으면 화남");
			TemperamentContext later = sitting;
			later.Now = 3f;
			Assert.Greater(TowardPlayer(greedy.Steer(later, memory), later), 0.9f, "화난 동안은 쫓음");
		}

		[Test]
		public void Avenger_rages_and_lunges_after_nearby_fall()
		{
			AvengerTemperament avenger = Make<AvengerTemperament>();
			TemperamentMemory memory = Fresh();
			TemperamentContext calm = At(6f, 0f);
			TemperamentStep slow = avenger.Steer(calm, memory);
			Assert.Less(slow.Speed, 1f, "평소엔 느림");
			TemperamentContext fallen = At(6f, 1f);
			fallen.SinceFallen = 0.1f;
			TemperamentStep notice = avenger.Steer(fallen, memory);
			Assert.Greater(notice.Punch, 1f, "쓰러지면 알아채 부풂");
			Assert.IsTrue(AvengerTemperament.IsRaging(memory, 1f));
			TemperamentContext lunge = At(6f, 2f);
			TemperamentStep jump = avenger.Steer(lunge, memory);
			Assert.Greater(jump.BurstSeconds, 0f, "화나면 덤빔");
			Assert.Greater(Vector3.Dot(jump.BurstVelocity.normalized, lunge.ToPlayer.normalized), 0.9f);
			Assert.IsFalse(AvengerTemperament.IsRaging(memory, 7f), "시간이 지나면 풀림");
			Assert.Less(avenger.Steer(At(6f, 7f), memory).Speed, 1f);
		}

		[Test]
		public void Patrol_walks_between_ends_naps_and_notices()
		{
			PatrolTemperament patrol = Make<PatrolTemperament>();
			// Seed 0 이면 길은 x 축. 태어난 자리 (0,0,0) 에서 +4, -4. 주인공은 멀리 (0,0,50)
			TemperamentMemory memory = Fresh(0f);
			memory.Side = 1f;
			Vector3 self = Vector3.zero;
			float now = 0f;
			for (int i = 0; i < 40 && PatrolTemperament.IsNapping(memory) == false; i++, now += 0.1f)
			{
				TemperamentStep step = patrol.Steer(Far(self, now), memory);
				Assert.Less(step.Speed, 1f, "순찰은 천천히");
				self += step.Direction.normalized * 0.25f;
			}
			Assert.IsTrue(PatrolTemperament.IsNapping(memory), "끝에 닿으면 낮잠");
			Assert.Greater(self.x, 3.4f, "+x 끝");
			TemperamentContext napping = Far(self, now + 0.5f);
			napping.Player = self + new Vector3(0f, 0f, 2f);
			Assert.IsTrue(patrol.Steer(napping, memory).HoldsAttack, "낮잠 중엔 곁을 지나가도 못 봄");
			TemperamentStep turn = patrol.Steer(Far(self, now + 3f), memory);
			Assert.Less(turn.Direction.x, -0.9f, "깨면 반대 끝으로");
			TemperamentContext seen = Far(self, now + 3.1f);
			seen.Player = self + new Vector3(0f, 0f, 3f);
			Assert.Greater(patrol.Steer(seen, memory).Punch, 1f, "걷다 보이면 놀라 쫓음");
		}

		private static TemperamentContext Far(Vector3 self, float now)
		{
			TemperamentContext context = At(self.x, now, self.z);
			context.Player = new Vector3(0f, 0f, 50f);
			return context;
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
