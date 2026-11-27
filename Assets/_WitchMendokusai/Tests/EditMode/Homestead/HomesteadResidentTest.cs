using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 주민 호감도 곡선. 챙기면 몇 주, 의뢰만 해도 언젠가 닿음
	public class HomesteadResidentTest
	{
		private const int RUNS = 120;
		private const int NEVER = 999;
		private static readonly int[] SEEDS = { 11, 23, 37, 41, 53 };

		[Test]
		public void Affinity_step_counts_are_two_three_and_five_and_rise()
		{
			HashSet<int> counts = new();
			foreach (ResidentDef resident in HomesteadTestKit.LoadCatalog().Residents)
			{
				counts.Add(resident.AffinitySteps.Count);
				for (int i = 1; i < resident.AffinitySteps.Count; i++)
				{
					Assert.That(resident.AffinitySteps[i], Is.GreaterThan(resident.AffinitySteps[i - 1]), resident.Id);
				}
			}
			Assert.That(counts, Is.EquivalentTo(new[] { 2, 3, 5 }));
		}

		// 챙기는 손: 시설 하루 한 번과 가진 것 중 가장 반기는 선물 하루 한 번. 의뢰만: 둘 다 안 함
		// 갇힌 사람은 판마다 이 확률로 구함
		[TestCase(0.5)]
		public void Caring_reaches_the_last_step_in_weeks_and_requests_alone_still_get_there(double rescueChance)
		{
			Dictionary<string, List<int>> caring = MaxStepDays(true, rescueChance);
			Dictionary<string, List<int>> plain = MaxStepDays(false, rescueChance);
			foreach (KeyValuePair<string, List<int>> pair in caring)
			{
				int caringDays = Median(pair.Value);
				int plainDays = Median(plain[pair.Key]);
				TestContext.WriteLine($"{pair.Key,-11} 챙기면 {string.Join("/", pair.Value)}일, 의뢰만 {string.Join("/", plain[pair.Key])}일");
				Assert.That(caringDays, Is.InRange(7, 30), $"{pair.Key}: 챙기면 몇 주");
				Assert.That(plainDays, Is.LessThan(NEVER), $"{pair.Key}: 의뢰만 해도 닿음");
				Assert.That(plainDays, Is.GreaterThanOrEqualTo(caringDays * 2), $"{pair.Key}: 챙기는 손이 두 배 넘게 빠름");
			}
		}

		// 확정 f14-2: 다섯이 10판 안에 다 오지 않게 시련소 사람을 늦춤
		// 봇의 보스는 4, 9 번째 판 고정이라 보스 주기를 따로 더해 잼. every 0 은 봇 그대로
		[Test]
		public void Last_resident_does_not_arrive_within_ten_runs()
		{
			foreach (int every in new[] { 0, 5, 3 })
			{
				List<int> lastRuns = new();
				foreach (int seed in SEEDS)
				{
					Dictionary<string, int> arrived = ArrivalRuns(seed, every);
					int last = 0;
					foreach (int run in arrived.Values)
					{
						last = Math.Max(last, run);
					}
					lastRuns.Add(last);
					TestContext.WriteLine($"보스 주기 {every}, 씨앗 {seed}: {string.Join(", ", ArrivalText(arrived))}");
				}
				TestContext.WriteLine($"보스 주기 {every}: 마지막 도착 판 {string.Join("/", lastRuns)}, 중앙 {Median(lastRuns)}");
				if (every != 3)
				{
					Assert.That(Median(lastRuns), Is.GreaterThan(10), $"보스 주기 {every}: 다섯이 10판 안에 다 옴");
					Assert.That(Median(lastRuns), Is.LessThan(NEVER), $"보스 주기 {every}: 끝내 안 오는 주민");
				}
			}
		}

		private static IEnumerable<string> ArrivalText(Dictionary<string, int> arrived)
		{
			foreach (KeyValuePair<string, int> pair in arrived)
			{
				yield return $"{pair.Key} {(pair.Value == NEVER ? "-" : pair.Value.ToString())}";
			}
		}

		// 주민마다 처음 와 있던 판 (1부터). 120판 안에 안 오면 NEVER
		private static Dictionary<string, int> ArrivalRuns(int seed, int bossEvery)
		{
			MethodInfo playDungeon = typeof(HomesteadPlayTest).GetMethod("PlayDungeon", BindingFlags.NonPublic | BindingFlags.Static);
			MethodInfo playTown = typeof(HomesteadPlayTest).GetMethod("PlayTown", BindingFlags.NonPublic | BindingFlags.Static);
			HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
			purse.Nyang = 300;
			Random random = new(seed + 5);
			Dictionary<string, int> arrived = new();
			foreach (ResidentDef resident in sim.Catalog.Residents)
			{
				arrived[resident.Id] = NEVER;
			}
			for (int run = 0; run < RUNS; run++)
			{
				playDungeon.Invoke(null, new object[] { sim, purse, random, run });
				bool botBoss = run == 4 || run == 9;
				if (bossEvery > 0 && run % bossEvery == bossEvery - 1 && botBoss == false)
				{
					sim.NotifyEvent(HomesteadSim.BOSS_EVENT);
				}
				List<string> wanted = sim.WantedRescues();
				if (wanted.Count > 0 && random.NextDouble() < 0.5)
				{
					sim.NotifyRescue(wanted[0]);
				}
				playTown.Invoke(null, new object[] { sim, random });
				while (sim.CanUpgradeTownHall().Ok)
				{
					sim.UpgradeTownHall();
				}
				foreach (ResidentDef resident in sim.Catalog.Residents)
				{
					Visit(sim, resident, false);
					if (sim.HasArrived(resident.Id) && arrived[resident.Id] == NEVER)
					{
						arrived[resident.Id] = run + 1;
					}
				}
			}
			return arrived;
		}

		private static int Median(List<int> days)
		{
			List<int> sorted = new(days);
			sorted.Sort();
			return sorted[sorted.Count / 2];
		}

		// 주민마다 도착한 날부터 마지막 단계까지 걸린 날. 못 닿으면 NEVER
		private static Dictionary<string, List<int>> MaxStepDays(bool caring, double rescueChance)
		{
			// 마을 봇은 판 수 시험의 것을 그대로 씀. 이름이 바뀌면 여기서 바로 깨짐
			MethodInfo playDungeon = typeof(HomesteadPlayTest).GetMethod("PlayDungeon", BindingFlags.NonPublic | BindingFlags.Static);
			MethodInfo playTown = typeof(HomesteadPlayTest).GetMethod("PlayTown", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(playDungeon, Is.Not.Null);
			Assert.That(playTown, Is.Not.Null);

			Dictionary<string, List<int>> result = new();
			foreach (int seed in SEEDS)
			{
				HomesteadSim sim = HomesteadTestKit.MakeSim(out HomesteadTestKit.FakePurse purse, seed);
				purse.Nyang = 300;
				Random random = new(seed + 5);
				Dictionary<string, long> arrivedDay = new();
				Dictionary<string, int> doneDays = new();
				for (int run = 0; run < RUNS; run++)
				{
					playDungeon.Invoke(null, new object[] { sim, purse, random, run });
					List<string> wanted = sim.WantedRescues();
					if (wanted.Count > 0 && random.NextDouble() < rescueChance)
					{
						sim.NotifyRescue(wanted[0]);
					}
					playTown.Invoke(null, new object[] { sim, random });
					while (sim.CanUpgradeTownHall().Ok)
					{
						sim.UpgradeTownHall();
					}
					foreach (ResidentDef resident in sim.Catalog.Residents)
					{
						Visit(sim, resident, caring);
						if (sim.HasArrived(resident.Id) && arrivedDay.ContainsKey(resident.Id) == false)
						{
							arrivedDay[resident.Id] = sim.Day;
						}
						if (doneDays.ContainsKey(resident.Id) == false && sim.ResidentOf(resident.Id).ClaimedSteps == resident.AffinitySteps.Count)
						{
							doneDays[resident.Id] = (int)(sim.Day - arrivedDay[resident.Id]);
						}
					}
				}
				foreach (ResidentDef resident in sim.Catalog.Residents)
				{
					if (result.ContainsKey(resident.Id) == false)
					{
						result[resident.Id] = new List<int>();
					}
					result[resident.Id].Add(doneDays.TryGetValue(resident.Id, out int days) ? days : NEVER);
				}
			}
			return result;
		}

		// 부탁 길은 낼 수 있으면 바로 냄. 챙기는 손은 시설과 선물까지
		private static void Visit(HomesteadSim sim, ResidentDef resident, bool caring)
		{
			if (sim.HasArrived(resident.Id) == false)
			{
				foreach (RecruitDef recruit in resident.Recruits)
				{
					if (recruit.Kind == RecruitKind.Favor && sim.CanPay(recruit.Cost).Ok)
					{
						sim.PayFavor(resident.Id, recruit);
					}
				}
				return;
			}
			if (caring == false)
			{
				return;
			}
			if (sim.IsBuilt(resident.Building))
			{
				sim.UseFacility(resident.Building);
			}
			string best = string.Empty;
			int bestPoints = 0;
			foreach (GoodsDef goods in sim.Catalog.Goods)
			{
				int points = sim.Count(goods.Id) > 0 ? sim.GiftPoints(resident.Id, goods.Id) : 0;
				if (points > bestPoints)
				{
					bestPoints = points;
					best = goods.Id;
				}
			}
			if (best.Length > 0 && sim.CanGiftToday(resident.Id))
			{
				sim.Gift(resident.Id, best);
			}
		}
	}
}
