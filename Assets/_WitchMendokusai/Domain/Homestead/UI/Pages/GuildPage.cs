using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 길드 게시판. 의뢰, 사람, 인형에게 맡길 일
	public sealed class GuildPage : HomesteadPage
	{
		private const int TAB_REQUESTS = 0;
		private const int TAB_PEOPLE = 1;
		private const int GIFT_CHOICES = 5;

		private static readonly string[] TABS = { "의뢰", "사람", "맡길 일" };

		public GuildPage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "길드 게시판";
		protected override string IconKey => "board";
		public override IReadOnlyList<string> Tabs => TABS;

		public override string Status
		{
			get
			{
				string level = $"길드 레벨 {Sim.GuildLevel}";
				return Sim.TryGetGuildProgress(out int current, out int needed) ? $"{level}, {current} / {needed}" : level;
			}
		}

		public override string EmptyText(int tab) => tab == TAB_REQUESTS ? "오늘 의뢰를 다 끝냈습니다. 새 의뢰는 내일 붙습니다." : string.Empty;

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			switch (tab)
			{
				case TAB_REQUESTS:
					foreach (RequestState request in Sim.State.Requests)
						entries.Add(RequestEntry(request));
					break;
				case TAB_PEOPLE:
					foreach (ResidentDef resident in Catalog.Residents)
						entries.Add(ResidentEntry(resident));
					break;
				default:
					foreach (JobDef job in Catalog.Jobs)
						entries.Add(JobEntry(job));
					break;
			}
			return entries;
		}

		// ---- 의뢰

		private HomesteadEntry RequestEntry(RequestState request)
		{
			RequestDef template = Catalog.Get<RequestDef>(request.Template);
			GoodsDef goods = Catalog.Get<GoodsDef>(request.Goods);
			bool ready = Sim.Count(request.Goods) >= request.Amount;
			return new HomesteadEntry
			{
				Id = request.Uid.ToString(),
				Icon = Art.IconOf(goods),
				Name = template.Name,
				Sub = $"{goods.Name} {request.Amount}",
				Count = $"{Sim.Count(request.Goods)} / {request.Amount}",
				IsMarked = ready,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(goods), Title = template.Name, Note = request.Resident.Length > 0 ? $"{Catalog.Get<ResidentDef>(request.Resident).Name}의 부탁" : "길드의 부탁" };
					bool isBonus = Sim.State.RequestsDone < Catalog.Tuning.RequestBonusCount;
					detail.Lines.Add($"받는 것: {request.Nyang}냥, 길드 {(isBonus ? request.Exp * 2 : request.Exp)}");
					if (request.Resident.Length > 0)
						detail.Lines.Add("호감도가 오릅니다.");
					detail.Lines.Add("기한이 없습니다.");
					AddGoods(detail, request.Goods, request.Amount);
					HomesteadResult can = ready ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, request.Goods);
					AddAction(detail, "건네기", can, () => Sim.Deliver(request.Uid), UiSfxEvent.Purchase);
					AddAction(detail, "떼기", HomesteadResult.Success, () => Sim.Dismiss(request.Uid), UiSfxEvent.Cancel);
					return detail;
				},
			};
		}

		// ---- 사람

		private HomesteadEntry ResidentEntry(ResidentDef resident)
		{
			bool arrived = Sim.HasArrived(resident.Id);
			return new HomesteadEntry
			{
				Id = resident.Id,
				Icon = Art.Icon(resident.Id),
				Name = resident.Name,
				Sub = arrived ? $"호감 {Sim.AffinityStep(resident.Id)} / {resident.AffinitySteps.Count}" : "아직 오지 않음",
				IsDim = arrived == false,
				IsUnknown = arrived == false,
				IsMarked = arrived ? Sim.CanGiftToday(resident.Id) : CanPayAnyFavor(resident),
				Detail = () => arrived ? PersonDetail(resident) : AbsentDetail(resident),
			};
		}

		private bool CanPayAnyFavor(ResidentDef resident) =>
			resident.Recruits.Exists(recruit => recruit.Kind == RecruitKind.Favor && Sim.CanPay(recruit.Cost).Ok);

		private HomesteadDetail AbsentDetail(ResidentDef resident)
		{
			HomesteadDetail detail = new() { Icon = Art.Icon(resident.Id), Title = resident.Name, Note = $"{Catalog.Get<BuildingDef>(resident.Building).Name}을 맡을 사람" };
			detail.Lines.Add("오는 길 (하나면 됩니다)");
			foreach (RecruitDef recruit in resident.Recruits)
			{
				detail.Lines.Add($"  {recruit.Hint}");
				if (recruit.Kind != RecruitKind.Favor)
					continue;
				detail.CostTitle = "부탁";
				AddCost(detail, recruit.Cost);
				AddAction(detail, "부탁 들어주기", Sim.CanPay(recruit.Cost), () => Sim.PayFavor(resident.Id, recruit), UiSfxEvent.LevelUp);
			}
			return detail;
		}

		private HomesteadDetail PersonDetail(ResidentDef resident)
		{
			ResidentState state = Sim.ResidentOf(resident.Id);
			HomesteadDetail detail = new() { Icon = Art.Icon(resident.Id), Title = resident.Name, Note = Catalog.Get<BuildingDef>(resident.Building).Name };
			int step = Sim.AffinityStep(resident.Id);
			if (step < resident.AffinitySteps.Count)
			{
				int from = step == 0 ? 0 : resident.AffinitySteps[step - 1];
				detail.Bar = (state.Affinity - from) / (float)(resident.AffinitySteps[step] - from);
				detail.BarText = $"호감 {step}단, {state.Affinity} / {resident.AffinitySteps[step]}";
				detail.Lines.Add($"다음 단: {HomesteadText.Reward(resident.AffinityRewards[step], Catalog)}");
			}
			else
			{
				detail.Bar = 1f;
				detail.BarText = "가장 가까운 사이";
			}
			detail.Lines.Add(Sim.CanGiftToday(resident.Id) ? "오늘 선물을 하나 건넬 수 있습니다." : "오늘은 선물을 건넸습니다.");
			foreach (GoodsDef goods in GiftChoices(resident))
			{
				HomesteadResult can = Sim.CanGiftToday(resident.Id) ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.DailyLimit);
				AddAction(detail, $"{goods.Name} 건네기  +{Sim.GiftPoints(resident.Id, goods.Id)}", can, () => Sim.Gift(resident.Id, goods.Id), UiSfxEvent.Purchase);
			}
			return detail;
		}

		// 가진 것 중 점수가 높고 값이 싼 순. 귀한 것을 무심코 건네지 않게
		private List<GoodsDef> GiftChoices(ResidentDef resident)
		{
			List<GoodsDef> owned = Catalog.Goods.FindAll(goods => goods.Kind != GoodsKind.Seed && Sim.Count(goods.Id) > 0);
			owned.Sort((first, second) =>
			{
				int byPoints = Sim.GiftPoints(resident.Id, second.Id).CompareTo(Sim.GiftPoints(resident.Id, first.Id));
				return byPoints != 0 ? byPoints : first.Price.CompareTo(second.Price);
			});
			return owned.GetRange(0, Mathf.Min(GIFT_CHOICES, owned.Count));
		}

		// ---- 맡길 일

		private HomesteadEntry JobEntry(JobDef job)
		{
			bool built = Sim.IsBuilt(job.Building);
			Doll worker = WorkerOf(job);
			int ready = Sim.JobReady(job.Id);
			return new HomesteadEntry
			{
				Id = job.Id,
				Icon = worker != null ? worker.Sprite : Art.Icon(job.Building),
				Name = job.Name,
				Sub = built == false ? $"{Catalog.Get<BuildingDef>(job.Building).Name} 필요" : worker != null ? worker.Name : "맡길 수 있음",
				Count = worker != null && job.Yields.Count > 0 ? $"{ready} / {Sim.JobCap(job)}" : string.Empty,
				IsDim = built == false,
				IsMarked = ready > 0,
				Detail = () => JobDetail(job),
			};
		}

		private Doll WorkerOf(JobDef job) => Sim.IsStaffed(job.Id) ? SOHelper.GetDoll(Sim.State.Jobs[job.Id].DollId) : null;

		private HomesteadDetail JobDetail(JobDef job)
		{
			Doll worker = WorkerOf(job);
			HomesteadDetail detail = new() { Icon = worker != null ? worker.Sprite : Art.Icon(job.Building), Title = job.Name, Note = worker != null ? $"{worker.Name}에게 맡김" : Catalog.Get<BuildingDef>(job.Building).Name };
			if (job.Note.Length > 0)
				detail.Lines.Add(job.Note);
			if (job.Yields.Count > 0)
			{
				List<string> names = job.Yields.ConvertAll(drop => Catalog.Get<GoodsDef>(drop.Goods).Name);
				detail.Lines.Add($"얻는 것: {string.Join(", ", names)}");
				detail.Lines.Add($"{HomesteadText.Minutes(job.IntervalMinutes)}마다 하나, {Sim.JobCap(job)}까지 쌓임");
				detail.Bar = Sim.JobReady(job.Id) / (float)Mathf.Max(1, Sim.JobCap(job));
				detail.BarText = $"{Sim.JobReady(job.Id)} / {Sim.JobCap(job)}";
				HomesteadResult can = Sim.JobReady(job.Id) > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotReady);
				AddAction(detail, "거두기", can, () => Done(() => Sim.CollectJob(job.Id)), UiSfxEvent.Purchase);
			}
			if (worker != null)
				AddAction(detail, "그만두게 하기", HomesteadResult.Success, () => Sim.Unassign(job.Id), UiSfxEvent.Cancel);
			SOHelper.ForEach<Doll>(doll =>
			{
				if (doll.IsControllable == false || doll == worker)
					return;
				string current = Sim.JobOf(doll.ID);
				string label = current.Length == 0 ? $"{doll.Name}에게" : $"{doll.Name}에게 ({Catalog.Get<JobDef>(current).Name} 중)";
				HomesteadResult can = Sim.IsBuilt(job.Building) ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotBuilt, job.Building);
				AddAction(detail, label, can, () => Sim.Assign(job.Id, doll.ID));
			});
			return detail;
		}
	}
}
