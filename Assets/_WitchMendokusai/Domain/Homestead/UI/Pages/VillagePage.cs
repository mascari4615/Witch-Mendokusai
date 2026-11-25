using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 회관. 회관 키우기와 건물 공사, 도구, 집 넓히기
	public sealed class VillagePage : HomesteadPage
	{
		private const int TAB_BUILDINGS = 0;
		private const int TAB_TOOLS = 1;
		private const string HALL_ID = "townhall";
		private const string HOUSE_ID = "house";

		private static readonly string[] TABS = { "공사", "도구" };

		public VillagePage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "회관";
		protected override string IconKey => HALL_ID;
		public override IReadOnlyList<string> Tabs => TABS;
		public override string Status => $"{Sim.TownHallDef(Sim.State.TownHall).Name}, 공사 {Sim.BusyWorks} / {Sim.WorkSlots}";

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			if (tab == TAB_TOOLS)
			{
				foreach (ToolDef tool in Catalog.Tools)
					entries.Add(ToolEntry(tool));
				return entries;
			}
			entries.Add(HallEntry());
			entries.Add(HouseEntry());
			foreach (BuildingDef building in Catalog.Buildings)
				entries.Add(BuildingEntry(building));
			return entries;
		}

		private HomesteadEntry HallEntry()
		{
			bool canRaise = Sim.CanUpgradeTownHall().Ok;
			return new HomesteadEntry
			{
				Id = HALL_ID,
				Icon = Art.Icon(HALL_ID),
				Name = Sim.TownHallDef(Sim.State.TownHall).Name,
				Sub = $"회관 {Sim.State.TownHall}단",
				IsMarked = canRaise,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(HALL_ID), Title = Sim.TownHallDef(Sim.State.TownHall).Name, Note = Sim.TownHallDef(Sim.State.TownHall).Note };
					if (Sim.TryGetNextTownHall(out TownHallDef next) == false)
					{
						detail.Lines.Add("마을이 다 되살아났습니다.");
						return detail;
					}
					detail.Lines.Add($"다음: {next.Name}");
					detail.Lines.Add(next.Note);
					detail.Lines.Add($"밭 {Sim.TownHallDef(Sim.State.TownHall).PlotCount} → {next.PlotCount}, 공사 칸 {Sim.WorkSlots} → {next.WorkSlots}");
					AddCost(detail, next.Cost);
					AddAction(detail, "회관 키우기", Sim.CanUpgradeTownHall(), () => Saved(Sim.UpgradeTownHall()), UiSfxEvent.LevelUp);
					return detail;
				},
			};
		}

		private HomesteadEntry HouseEntry()
		{
			return new HomesteadEntry
			{
				Id = HOUSE_ID,
				Icon = Art.Icon(HOUSE_ID),
				Name = "집",
				Sub = Sim.HouseTier.Name,
				IsMarked = Sim.CanExpandHouse().Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(HOUSE_ID), Title = "집", Note = Sim.HouseTier.Name };
					detail.Lines.Add($"방 {Sim.HouseTier.Width} x {Sim.HouseTier.Depth}, 가구 {Sim.State.Placed.Count} / {Sim.HouseTier.PieceLimit}");
					if (Sim.TryGetNextHouseTier(out HouseTierDef next) == false)
						return detail;
					detail.Lines.Add($"다음: {next.Name}. 방 {next.Width} x {next.Depth}, 가구 {next.PieceLimit}");
					if (Sim.State.TownHall < next.TownHall)
						detail.Lines.Add($"회관 {next.TownHall}단부터");
					AddCost(detail, next.Cost);
					AddAction(detail, "넓히기", Sim.CanExpandHouse(), () => Saved(Sim.ExpandHouse()), UiSfxEvent.LevelUp);
					return detail;
				},
			};
		}

		private HomesteadEntry BuildingEntry(BuildingDef building)
		{
			int level = Sim.BuildingLevel(building.Id);
			WorkState work = Sim.WorkOn(building.Id);
			bool isDone = work != null && Sim.IsWorkDone(work);
			return new HomesteadEntry
			{
				Id = building.Id,
				Icon = Art.Icon(building.Id),
				Name = building.Name,
				Sub = SubText(building, work, isDone),
				Count = level > 0 ? $"{level}단" : string.Empty,
				IsDim = level == 0 && work == null,
				IsMarked = isDone || Sim.CanBuild(building.Id).Ok,
				Detail = () => BuildingDetail(building),
			};
		}

		private string SubText(BuildingDef building, WorkState work, bool isDone)
		{
			if (work != null)
				return isDone ? "다 됐습니다. 가림막을 걷으러 가세요" : $"공사 중, {HomesteadText.Minutes(work.ReadyAt - Sim.State.Minutes)}";
			if (Sim.IsBuilt(building.Id) == false)
				return Sim.State.TownHall < building.TownHall ? $"회관 {building.TownHall}단부터" : building.Note;
			return Sim.IsOpen(building.Id) ? building.Note : "맡을 사람을 기다립니다";
		}

		private HomesteadDetail BuildingDetail(BuildingDef building)
		{
			HomesteadDetail detail = new() { Icon = Art.Icon(building.Id), Title = building.Name, Note = building.Note };
			int level = Sim.BuildingLevel(building.Id);
			if (level > 0)
				detail.Lines.Add($"지금: {building.Levels[level - 1].Effect}");
			if (building.Resident.Length > 0 && Sim.HasArrived(building.Resident) == false)
			{
				detail.Lines.Add("맡을 사람이 오면 열립니다.");
				foreach (RecruitDef recruit in Catalog.Get<ResidentDef>(building.Resident).Recruits)
					detail.Lines.Add($"  {recruit.Hint}");
			}

			WorkState work = Sim.WorkOn(building.Id);
			if (work != null)
			{
				detail.Lines.Add("던전에 한 번 다녀오면 바로 끝납니다.");
				detail.Bar = Sim.IsWorkDone(work) ? 1f : 1f - (work.ReadyAt - Sim.State.Minutes) / (float)building.Levels[work.Level - 1].Minutes;
				detail.BarText = Sim.IsWorkDone(work) ? "다 됐습니다" : HomesteadText.Minutes(work.ReadyAt - Sim.State.Minutes);
				return detail;
			}
			if (Sim.TryGetNextLevel(building.Id, out BuildingLevelDef next) == false)
				return detail;
			detail.Lines.Add($"다음: {next.Effect}");
			int needHall = System.Math.Max(next.TownHall, building.TownHall);
			if (Sim.State.TownHall < needHall)
				detail.Lines.Add($"회관 {needHall}단부터");
			detail.Lines.Add($"공사 {HomesteadText.Minutes(next.Minutes)}. 던전에 다녀오면 바로");
			AddCost(detail, next.Cost);
			AddAction(detail, level == 0 ? "짓기" : "키우기", Sim.CanBuild(building.Id), () => Saved(Sim.StartBuild(building.Id)), UiSfxEvent.Purchase);
			return detail;
		}

		private HomesteadEntry ToolEntry(ToolDef tool)
		{
			ToolTierDef current = tool.Tiers[Sim.ToolTier(tool.Kind)];
			return new HomesteadEntry
			{
				Id = tool.Id,
				Icon = Art.Icon(tool.Id),
				Name = current.Name,
				Sub = $"{tool.Name} {Sim.ToolTier(tool.Kind) + 1} / {tool.Tiers.Count}",
				IsMarked = Sim.CanUpgradeTool(tool.Kind).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(tool.Id), Title = current.Name, Note = tool.Name };
					if (Sim.TryGetNextToolTier(tool.Kind, out ToolTierDef next) == false)
					{
						detail.Lines.Add("더 좋은 것이 없습니다.");
						return detail;
					}
					detail.Lines.Add($"다음: {next.Name}");
					detail.Lines.Add($"힘 {current.Power} → {next.Power}");
					if (Sim.State.TownHall < next.TownHall)
						detail.Lines.Add($"회관 {next.TownHall}단부터");
					AddCost(detail, next.Cost);
					AddAction(detail, "벼리기", Sim.CanUpgradeTool(tool.Kind), () => Saved(Sim.UpgradeTool(tool.Kind)), UiSfxEvent.LevelUp);
					return detail;
				},
			};
		}

		// 되돌릴 수 없는 값을 치른 자리는 바로 저장
		private HomesteadResult Saved(HomesteadResult result)
		{
			if (result.Ok)
				Manager.Save();
			return result;
		}
	}
}
