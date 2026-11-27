using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 마을 관리. 타운홀 (재료로 직접 올림) 과 길드 레벨 (경험치) 두 축, 건물 공사
	public sealed partial class HomesteadSim
	{
		public TownHallDef TownHallDef(int tier) => Catalog.TownHalls[tier - 1];

		public bool TryGetNextTownHall(out TownHallDef next)
		{
			next = State.TownHall < Catalog.TownHalls.Count ? Catalog.TownHalls[State.TownHall] : null;
			return next != null;
		}

		public HomesteadResult CanUpgradeTownHall()
		{
			return TryGetNextTownHall(out TownHallDef next) ? CanPay(next.Cost) : HomesteadResult.Fail(HomesteadFailure.AtMaxLevel);
		}

		public HomesteadResult UpgradeTownHall()
		{
			HomesteadResult result = CanUpgradeTownHall();
			if (result.Ok == false)
			{
				return result;
			}
			TryGetNextTownHall(out TownHallDef next);
			Pay(next.Cost);
			State.TownHall = next.Tier;
			EnsurePlots();
			Grant(next.Reward);
			CheckRecruits();
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public int BuildingLevel(string buildingId) => State.Buildings.TryGetValue(buildingId, out int level) ? level : 0;

		public bool IsBuilt(string buildingId) => BuildingLevel(buildingId) > 0;

		// 건물과 사람이 다 있어야 기능이 열림. 사람이 필요 없는 건물은 짓기만
		public bool IsOpen(string buildingId)
		{
			if (IsBuilt(buildingId) == false)
			{
				return false;
			}
			string resident = Catalog.Get<BuildingDef>(buildingId).Resident;
			return resident.Length == 0 || HasArrived(resident);
		}

		public WorkState WorkOn(string buildingId)
		{
			foreach (WorkState work in State.Works)
			{
				if (work.Building == buildingId)
				{
					return work;
				}
			}
			return null;
		}

		public int WorkSlots => TownHallDef(State.TownHall).WorkSlots;

		public bool IsWorkDone(WorkState work) => State.Minutes >= work.ReadyAt;

		// 다 되고 가림막만 남은 자리는 칸을 차지하지 않음
		public int BusyWorks => State.Works.FindAll(work => IsWorkDone(work) == false).Count;

		public bool TryGetNextLevel(string buildingId, out BuildingLevelDef next)
		{
			List<BuildingLevelDef> levels = Catalog.Get<BuildingDef>(buildingId).Levels;
			int level = BuildingLevel(buildingId);
			next = level < levels.Count ? levels[level] : null;
			return next != null;
		}

		public HomesteadResult CanBuild(string buildingId)
		{
			if (TryGetNextLevel(buildingId, out BuildingLevelDef next) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.AtMaxLevel);
			}
			if (WorkOn(buildingId) != null)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			int neededTier = Math.Max(next.TownHall, Catalog.Get<BuildingDef>(buildingId).TownHall);
			if (State.TownHall < neededTier)
			{
				return TownHallTooLow(neededTier);
			}
			if (BusyWorks >= WorkSlots)
			{
				return HomesteadResult.Fail(HomesteadFailure.Busy);
			}
			return CanPay(next.Cost);
		}

		// 잠긴 이유에 필요한 회관 단을 실음. 화면이 "회관 3단 필요" 로 보임
		private static HomesteadResult TownHallTooLow(int neededTier) => HomesteadResult.Fail(HomesteadFailure.TownHallTooLow, neededTier.ToString());

		public HomesteadResult StartBuild(string buildingId)
		{
			HomesteadResult result = CanBuild(buildingId);
			if (result.Ok == false)
			{
				return result;
			}
			TryGetNextLevel(buildingId, out BuildingLevelDef next);
			Pay(next.Cost);
			State.Works.Add(new WorkState { Building = buildingId, Level = BuildingLevel(buildingId) + 1, ReadyAt = State.Minutes + next.Minutes });
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 던전에 한 번 다녀오면 벌인 공사가 모두 끝남. 기다림을 판으로 바꾼 자리
		public void NotifyExpedition()
		{
			foreach (WorkState work in State.Works)
			{
				work.ReadyAt = Math.Min(work.ReadyAt, State.Minutes);
			}
			MaybeBringExpeditionSeed();
			NotifyChanged();
		}

		public event Action<string> Unveiled = delegate { };

		// 가림막 걷기. 이때 단계가 오름
		public HomesteadResult Unveil(string buildingId)
		{
			WorkState work = WorkOn(buildingId);
			if (work == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, buildingId);
			}
			if (IsWorkDone(work) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady, buildingId);
			}
			State.Works.Remove(work);
			State.Buildings[buildingId] = work.Level;
			AddGuildExp(Catalog.Get<BuildingDef>(buildingId).Levels[work.Level - 1].GuildExp);
			EnsurePlots();
			CheckRecruits();
			Unveiled(buildingId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// ---- 길드 레벨

		public int GuildLevel
		{
			get
			{
				int level = 1;
				for (int i = 1; i < Tuning.GuildSteps.Count; i++)
				{
					if (State.GuildExp >= Tuning.GuildSteps[i])
					{
						level = i + 1;
					}
				}
				return level;
			}
		}

		public bool TryGetGuildProgress(out int current, out int needed)
		{
			int level = GuildLevel;
			current = State.GuildExp - Tuning.GuildSteps[level - 1];
			needed = level < Tuning.GuildSteps.Count ? Tuning.GuildSteps[level] - Tuning.GuildSteps[level - 1] : 0;
			return needed > 0;
		}

		public event Action<int> GuildLeveled = delegate { };

		private void AddGuildExp(int exp)
		{
			if (exp <= 0)
			{
				return;
			}
			int before = GuildLevel;
			State.GuildExp += exp;
			if (GuildLevel > before)
			{
				GuildLeveled(GuildLevel);
				CheckRecruits();
			}
		}

		// ---- 길드 의뢰판

		public int RequestSlotCount => Math.Min(Tuning.RequestSlotMax, Tuning.RequestSlots + (GuildLevel - 1) / Tuning.RequestSlotPerLevels);

		// 하루에 한 번 빈 칸만 채움. 못 끝낸 의뢰는 남음 (기한 없음)
		public void RefreshRequests()
		{
			if (State.RequestDay == Day && State.Requests.Count > 0)
			{
				return;
			}
			State.RequestDay = Day;
			List<RequestDef> open = new List<RequestDef>();
			foreach (RequestDef request in Catalog.Requests)
			{
				if (request.GuildLevel <= GuildLevel)
				{
					open.Add(request);
				}
			}
			while (State.Requests.Count < RequestSlotCount && open.Count > 0)
			{
				int pick = random.Next(open.Count);
				RequestDef template = open[pick];
				open.RemoveAt(pick);
				if (State.Requests.Exists(request => request.Template == template.Id) == false)
				{
					State.Requests.Add(MakeRequest(template));
				}
			}
			NotifyChanged();
		}

		private RequestState MakeRequest(RequestDef template)
		{
			string goodsId = template.Goods[random.Next(template.Goods.Count)];
			int amount = random.Next(template.Min, template.Max + 1);
			return new RequestState
			{
				Uid = State.NextRequestUid++,
				Template = template.Id,
				Goods = goodsId,
				Amount = amount,
				Nyang = (int)Math.Round(Catalog.Get<GoodsDef>(goodsId).Price * amount * template.Pay),
				Exp = template.Exp,
				Resident = RandomArrived(),
			};
		}

		public HomesteadResult Deliver(int uid)
		{
			RequestState request = State.Requests.Find(candidate => candidate.Uid == uid);
			if (request == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			if (Count(request.Goods) < request.Amount)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, request.Goods);
			}
			Take(request.Goods, request.Amount);
			Purse.Nyang += request.Nyang;
			State.Requests.Remove(request);
			AddGuildExp(State.RequestsDone < Tuning.RequestBonusCount ? request.Exp * 2 : request.Exp);
			State.RequestsDone++;
			if (request.Resident.Length > 0)
			{
				AddAffinity(request.Resident, Tuning.FavorPoints);
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 마음에 안 드는 의뢰 떼기. 벌 없음, 빈 칸은 다음 날
		public HomesteadResult Dismiss(int uid)
		{
			int removed = State.Requests.RemoveAll(request => request.Uid == uid);
			NotifyChanged();
			return removed > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.Unknown);
		}
	}
}
