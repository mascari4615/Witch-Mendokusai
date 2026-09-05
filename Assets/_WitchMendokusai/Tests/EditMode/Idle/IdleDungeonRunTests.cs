using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 던전 입장과 소탕 (layout.md 표 3, economy.md 표 2)
	///
	/// ★ 지키는 것: 입장권 한 장에 한 판, 보상은 그 던전이 주는 것만, 소탕은 한 판씩과 같음,
	///   스킬 던전은 아직 닫힘, 가방이 차면 장비만 안 들어옴
	/// </summary>
	public sealed class IdleDungeonRunTests
	{
		private static IdleState Ready(IdleTuning tuning)
		{
			IdleState state = new IdleState();
			IdleDungeons.Refill(state, tuning, 0L);
			return state;
		}

		[Test]
		public void GoldDungeon_PaysByIncome_AndSpendsOneTicket()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			long before = IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold);
			double want = IdleModel.IncomePerSecond(state, tuning) * tuning.DungeonGoldSeconds;

			Assert.IsTrue(IdleDungeons.TryEnter(state, tuning, IdleDungeonKind.Gold, out IdleDungeonReward got));

			Assert.AreEqual(1, got.Runs);
			Assert.AreEqual(want, got.Gold, 1e-6d);
			Assert.AreEqual(want, state.Resource, 1e-6d);
			Assert.AreEqual(before - 1L, IdleDungeons.TicketsOf(state, IdleDungeonKind.Gold));
		}

		[Test]
		public void BossDungeon_PaysShardsAndGear()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);

			Assert.IsTrue(IdleDungeons.TryEnter(state, tuning, IdleDungeonKind.Boss, out IdleDungeonReward got));

			Assert.AreEqual(tuning.DungeonBossShards, got.Shards);
			Assert.AreEqual(tuning.DungeonBossShards, state.PrestigeShards);
			Assert.AreEqual(tuning.DungeonBossGear, got.Gear);
			Assert.AreEqual(tuning.DungeonBossGear, state.Bag.Count);
			Assert.AreEqual(0d, got.Gold, 1e-9d, "보스 던전이 골드를 줬다");
		}

		[Test]
		public void GearDungeon_FillsTheBag_ButNotPastIt()
		{
			IdleTuning tuning = new IdleTuning();
			tuning.DungeonGearCount = 500L;
			IdleState state = Ready(tuning);
			int room = IdleShop.BagCapacityOf(state, tuning);

			Assert.IsTrue(IdleDungeons.TryEnter(state, tuning, IdleDungeonKind.Gear, out IdleDungeonReward got));

			Assert.AreEqual(room, got.Gear, "가방보다 많이 들어갔다");
			Assert.AreEqual(room, state.Bag.Count);
		}

		/// <summary>★ 스킬 재료가 아직 없어 스킬 던전은 닫혀 있다. 입장권도 안 쓴다</summary>
		[Test]
		public void SkillDungeon_IsClosed()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			long before = IdleDungeons.TicketsOf(state, IdleDungeonKind.Skill);

			Assert.IsFalse(IdleDungeons.IsOpen(IdleDungeonKind.Skill));
			Assert.IsFalse(IdleDungeons.TryEnter(state, tuning, IdleDungeonKind.Skill, out IdleDungeonReward _));
			Assert.AreEqual(before, IdleDungeons.TicketsOf(state, IdleDungeonKind.Skill));
		}

		/// <summary>★ 소탕은 남은 판을 한 번에. 한 판씩 누른 것과 결과가 같아야 한다</summary>
		[Test]
		public void Sweep_EqualsRunningEachByHand()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState byHand = Ready(tuning);
			double gold = 0d;
			int runs = 0;
			while (IdleDungeons.TryEnter(byHand, tuning, IdleDungeonKind.Gold, out IdleDungeonReward one))
			{
				gold += one.Gold;
				runs++;
			}

			IdleState swept = Ready(tuning);
			Assert.IsTrue(IdleDungeons.TrySweep(swept, tuning, IdleDungeonKind.Gold, out IdleDungeonReward all));

			Assert.AreEqual(tuning.TicketsPerDay, (long)runs, "손으로 돈 판 수가 하루 상한과 다르다");
			Assert.AreEqual(runs, all.Runs);
			Assert.AreEqual(gold, all.Gold, 1e-6d);
			Assert.AreEqual(byHand.Resource, swept.Resource, 1e-6d);
			Assert.AreEqual(0L, IdleDungeons.TicketsOf(swept, IdleDungeonKind.Gold));
		}

		[Test]
		public void Sweep_WithNoTickets_DoesNothing()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = Ready(tuning);
			IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Gold, out IdleDungeonReward _);

			Assert.IsFalse(IdleDungeons.TrySweep(state, tuning, IdleDungeonKind.Gold, out IdleDungeonReward again));
			Assert.AreEqual(0, again.Runs);
		}
	}
}
