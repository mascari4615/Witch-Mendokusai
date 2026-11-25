using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 주민 (건물을 맡는 사람) 과 인형 (일을 맡는 시민)
	public sealed partial class HomesteadSim
	{
		public event Action<string> Arrived = delegate { };
		public event Action<string, int> AffinityStepped = delegate { };

		public ResidentState ResidentOf(string residentId)
		{
			if (State.Residents.TryGetValue(residentId, out ResidentState state) == false)
			{
				state = new ResidentState();
				State.Residents.Add(residentId, state);
			}
			return state;
		}

		public bool HasArrived(string residentId) => State.Residents.TryGetValue(residentId, out ResidentState state) && state.Arrived;

		// ---- 영입

		public int EventCount(string key) => State.Events.TryGetValue(key, out int count) ? count : 0;

		// 마을 밖의 일 (보스 처치, 던전 구출) 을 알림
		public void NotifyEvent(string key)
		{
			State.Events[key] = EventCount(key) + 1;
			CheckRecruits();
			NotifyChanged();
		}

		public bool IsMet(RecruitDef recruit)
		{
			switch (recruit.Kind)
			{
				case RecruitKind.Rescue:
				case RecruitKind.Event:
					return EventCount(recruit.Key) >= Math.Max(1, recruit.Count);
				case RecruitKind.Build:
					return IsBuilt(recruit.Key);
				case RecruitKind.GuildLevel:
					return GuildLevel >= recruit.Count;
				default:
					// 부탁은 직접 건네야 이뤄짐
					return false;
			}
		}

		private void CheckRecruits()
		{
			foreach (ResidentDef resident in Catalog.Residents)
			{
				if (HasArrived(resident.Id))
				{
					continue;
				}
				foreach (RecruitDef recruit in resident.Recruits)
				{
					if (IsMet(recruit))
					{
						Arrive(resident.Id);
						break;
					}
				}
			}
		}

		public HomesteadResult PayFavor(string residentId, RecruitDef recruit)
		{
			if (HasArrived(residentId))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			if (recruit.Kind != RecruitKind.Favor)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			HomesteadResult result = CanPay(recruit.Cost);
			if (result.Ok == false)
			{
				return result;
			}
			Pay(recruit.Cost);
			Arrive(residentId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		private void Arrive(string residentId)
		{
			ResidentOf(residentId).Arrived = true;
			Arrived(residentId);
			AddGuildExp(Tuning.RecruitGuildExp);
		}

		private string RandomArrived()
		{
			List<string> arrived = new List<string>();
			foreach (ResidentDef resident in Catalog.Residents)
			{
				if (HasArrived(resident.Id))
				{
					arrived.Add(resident.Id);
				}
			}
			return arrived.Count == 0 ? string.Empty : arrived[random.Next(arrived.Count)];
		}

		// ---- 호감도. 부탁, 시설 이용, 선물로만 오름

		public int AffinityStep(string residentId)
		{
			ResidentDef resident = Catalog.Get<ResidentDef>(residentId);
			int affinity = ResidentOf(residentId).Affinity;
			int step = 0;
			foreach (int needed in resident.AffinitySteps)
			{
				if (affinity >= needed)
				{
					step++;
				}
			}
			return step;
		}

		private void AddAffinity(string residentId, int points)
		{
			ResidentDef resident = Catalog.Get<ResidentDef>(residentId);
			ResidentState state = ResidentOf(residentId);
			state.Affinity += points;
			int step = AffinityStep(residentId);
			while (state.ClaimedSteps < step)
			{
				RewardDef reward = resident.AffinityRewards[state.ClaimedSteps];
				state.ClaimedSteps++;
				Grant(reward);
				AddGuildExp(Tuning.AffinityGuildExp);
				AffinityStepped(residentId, state.ClaimedSteps);
			}
		}

		public int GiftPoints(string residentId, string goodsId)
		{
			ResidentDef resident = Catalog.Get<ResidentDef>(residentId);
			if (resident.Favorites.Contains(goodsId))
			{
				return Tuning.GiftFavoritePoints;
			}
			return resident.Likes.Contains(Catalog.Get<GoodsDef>(goodsId).Kind) ? Tuning.GiftLikedPoints : Tuning.GiftPlainPoints;
		}

		public bool CanGiftToday(string residentId) => ResidentOf(residentId).GiftDay != Day;

		public HomesteadResult Gift(string residentId, string goodsId)
		{
			if (HasArrived(residentId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NobodyThere, residentId);
			}
			if (CanGiftToday(residentId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.DailyLimit);
			}
			if (Count(goodsId) < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId);
			}
			Take(goodsId, 1);
			ResidentOf(residentId).GiftDay = Day;
			AddAffinity(residentId, GiftPoints(residentId, goodsId));
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 시설 이용. 하루 한 번만 점수
		public void UseFacility(string buildingId)
		{
			string resident = Catalog.Get<BuildingDef>(buildingId).Resident;
			if (resident.Length == 0 || HasArrived(resident) == false)
			{
				return;
			}
			ResidentState state = ResidentOf(resident);
			if (state.FacilityDay == Day)
			{
				return;
			}
			state.FacilityDay = Day;
			AddAffinity(resident, Tuning.FacilityPoints);
			NotifyChanged();
		}

		// ---- 인형의 일

		public bool HasPerk(string perk)
		{
			foreach (JobDef job in Catalog.Jobs)
			{
				if (job.Perk == perk && IsStaffed(job.Id))
				{
					return true;
				}
			}
			return false;
		}

		public bool IsStaffed(string jobId) => State.Jobs.TryGetValue(jobId, out JobState job) && job.DollId >= 0;

		public string JobOf(int dollId)
		{
			foreach (KeyValuePair<string, JobState> pair in State.Jobs)
			{
				if (pair.Value.DollId == dollId)
				{
					return pair.Key;
				}
			}
			return string.Empty;
		}

		// 인형 하나는 일 하나. 이미 다른 일을 하면 거기서 거둔 뒤 옮김
		public HomesteadResult Assign(string jobId, int dollId)
		{
			JobDef job = Catalog.Get<JobDef>(jobId);
			if (IsBuilt(job.Building) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, job.Building);
			}
			string current = JobOf(dollId);
			if (current == jobId)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			if (current.Length > 0)
			{
				Unassign(current);
			}
			if (IsStaffed(jobId))
			{
				Unassign(jobId);
			}
			State.Jobs[jobId] = new JobState { DollId = dollId, Since = State.Minutes };
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult Unassign(string jobId)
		{
			if (IsStaffed(jobId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NobodyThere, jobId);
			}
			CollectJob(jobId);
			State.Jobs.Remove(jobId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 건물 단계만큼 쌓이는 한도가 커짐. 한도에 닿으면 손해 없이 멈춤
		public int JobCap(JobDef job) => job.Cap * Math.Max(1, BuildingLevel(job.Building));

		public int JobReady(string jobId)
		{
			JobDef job = Catalog.Get<JobDef>(jobId);
			if (IsStaffed(jobId) == false || job.Yields.Count == 0)
			{
				return 0;
			}
			long elapsed = State.Minutes - State.Jobs[jobId].Since;
			return (int)Math.Min(JobCap(job), elapsed / job.IntervalMinutes);
		}

		public int CollectJob(string jobId)
		{
			int ready = JobReady(jobId);
			if (ready == 0)
			{
				return 0;
			}
			JobDef job = Catalog.Get<JobDef>(jobId);
			Dictionary<string, int> sum = new Dictionary<string, int>();
			for (int i = 0; i < ready; i++)
			{
				DropDef drop = Roll(job.Yields);
				sum[drop.Goods] = (sum.TryGetValue(drop.Goods, out int amount) ? amount : 0) + RollAmount(drop);
			}
			JobState state = State.Jobs[jobId];
			// 한도에 닿았으면 지금부터 다시, 아니면 남은 분을 이월
			state.Since = ready >= JobCap(job) ? State.Minutes : state.Since + (long)ready * job.IntervalMinutes;
			foreach (KeyValuePair<string, int> pair in sum)
			{
				Give(pair.Key, pair.Value);
			}
			NotifyChanged();
			return ready;
		}

		public int CollectAllJobs()
		{
			int total = 0;
			foreach (JobDef job in Catalog.Jobs)
			{
				total += CollectJob(job.Id);
			}
			return total;
		}
	}
}
