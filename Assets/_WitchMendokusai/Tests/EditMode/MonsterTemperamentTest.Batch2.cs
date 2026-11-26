using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 걷는 성격 2묶음 (점멸, 무리 짓기, 호위, 숨바꼭질, 탐냄, 복수, 낮잠 순찰). 감각 칸은 시험이 직접 채움
	// 도우미 (Make, Fresh, At, TowardPlayer) 는 MonsterTemperamentTest.cs
	public partial class MonsterTemperamentTest
	{
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
			TemperamentStep close = blink.Steer(At(1f, 10f), memory);
			Assert.IsFalse(close.HoldsAttack || close.Teleport, "너무 가까우면 점멸 안 하고 쫓음");
			TemperamentStep far = blink.Steer(At(30f, 10f), memory);
			Assert.IsFalse(far.HoldsAttack || far.Teleport, "너무 멀어도 점멸 안 함");
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
		public void Blink_restarts_windup_after_a_pause()
		{
			BlinkTemperament blink = Make<BlinkTemperament>();
			TemperamentMemory memory = Fresh(0f);
			blink.Steer(At(6f, 0f), memory);
			blink.Steer(At(6f, 3f), memory);
			// 예고 중 기절로 2초 멈췄다 풀림. 곧장 사라지지 않고 예고를 다시 보임
			TemperamentStep resumed = blink.Steer(At(6f, 5f), memory);
			Assert.IsFalse(resumed.Teleport, "멈췄다 풀리면 예고 없이 사라지지 않음");
			Assert.Greater(resumed.Punch, 1f, "예고를 다시 보임");
			Assert.IsTrue(blink.Steer(At(6f, 5.7f), memory).Teleport);
		}

		[Test]
		public void Guard_keeps_intercepting_until_player_backs_off_past_margin()
		{
			GuardTemperament guard = Make<GuardTemperament>();
			TemperamentMemory memory = Fresh();
			TemperamentContext context = At(6f, 0f, 3f);
			context.HasAlly = true;
			context.Ally = new Vector3(3.9f, 0f, 0f);
			guard.Steer(context, memory);
			context.Ally = new Vector3(4.5f, 0f, 0f);
			TemperamentStep held = guard.Steer(context, memory);
			Assert.Greater(TowardPlayer(held, context), 0.9f, "경계 바로 밖에서는 계속 막아섬");
			Assert.AreEqual(0f, held.Punch, "부풀림은 한 번");
			context.Ally = new Vector3(6f, 0f, 0f);
			Assert.Less(guard.Steer(context, memory).Direction.z, -0.5f, "충분히 물러나면 제자리로");
		}

		[Test]
		public void Avenger_notices_a_fall_it_missed_while_busy_but_only_once()
		{
			AvengerTemperament avenger = Make<AvengerTemperament>();
			TemperamentMemory memory = Fresh();
			// 1초에 쓰러졌는데 공격 중이라 1.8초에야 성격이 불림
			TemperamentContext late = At(6f, 1.8f);
			late.SinceFallen = 0.8f;
			Assert.Greater(avenger.Steer(late, memory).Punch, 1f, "놓친 처치도 끝난 뒤 알아챔");
			// 화가 풀린 뒤 같은 처치 소식은 다시 화나게 하지 않음
			TemperamentContext same = At(6f, 2.5f);
			same.SinceFallen = 1.5f;
			memory.UntilTime = 2f;
			Assert.Less(avenger.Steer(same, memory).Speed, 1f, "같은 처치에 두 번 화나지 않음");
		}

		[Test]
		public void Patrol_gives_up_a_blocked_leg()
		{
			PatrolTemperament patrol = Make<PatrolTemperament>();
			TemperamentMemory memory = Fresh(0f);
			memory.Side = 1f;
			// 벽에 막혀 제자리. 시간 상한 (8초) 이 지나면 닿은 것으로 치고 낮잠
			patrol.Steer(Far(Vector3.zero, 0f), memory);
			patrol.Steer(Far(Vector3.zero, 7f), memory);
			Assert.IsFalse(PatrolTemperament.IsNapping(memory));
			patrol.Steer(Far(Vector3.zero, 8.1f), memory);
			Assert.IsTrue(PatrolTemperament.IsNapping(memory), "막힌 길은 포기하고 낮잠");
		}
	}
}
