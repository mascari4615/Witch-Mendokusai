using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 박물관. 관마다 받침대 격자, 마지막 탭은 유물 손질
	public sealed class MuseumPage : HomesteadPage
	{
		private const string RESTORE_ID = "restore";

		private readonly List<string> tabs = new();

		public MuseumPage(HomesteadManager manager) : base(manager)
		{
			foreach (WingDef wing in Catalog.Wings)
				tabs.Add(wing.Name);
			tabs.Add("손질");
		}

		public override string Title => "박물관";
		protected override string IconKey => HomesteadSim.MUSEUM;
		public override IReadOnlyList<string> Tabs => tabs;
		public override bool IsGrid(int tab) => tab < Catalog.Wings.Count;

		public override string Status
		{
			get
			{
				int total = 0;
				foreach (WingDef wing in Catalog.Wings)
					total += Sim.ExhibitsOf(wing).Count;
				return $"기증 {Sim.State.Donated.Count} / {total}";
			}
		}

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			if (tab >= Catalog.Wings.Count)
			{
				entries.Add(RestoreEntry());
				return entries;
			}
			WingDef wing = Catalog.Wings[tab];
			foreach (GoodsDef goods in Sim.ExhibitsOf(wing))
				entries.Add(ExhibitEntry(wing, goods));
			return entries;
		}

		private HomesteadEntry ExhibitEntry(WingDef wing, GoodsDef goods)
		{
			bool donated = Sim.IsDonated(goods.Id);
			bool known = donated || Sim.IsKnown(goods.Id) || Sim.Count(goods.Id) > 0;
			bool canDonate = Sim.Count(goods.Id) > 0 && (donated == false || Sim.HasLargerFish(goods.Id));
			return new HomesteadEntry
			{
				Id = goods.Id,
				Icon = Art.IconOf(goods),
				Name = goods.Name,
				IsDim = donated == false,
				IsUnknown = known == false,
				IsMarked = canDonate,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = known ? Art.IconOf(goods) : null, Title = known ? goods.Name : "???", Note = wing.Note };
					detail.Lines.Add(StateLine(goods));
					AddRecord(detail, goods);
					AddWingProgress(detail, wing);
					if (donated == false)
					{
						HomesteadResult can = canDonate ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods);
						AddAction(detail, "기증하기", can, () => Sim.Donate(goods.Id), UiSfxEvent.Chest);
					}
					else if (Sim.HasLargerFish(goods.Id))
					{
						HomesteadResult can = Sim.Count(goods.Id) > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods);
						AddAction(detail, $"더 큰 것으로 바꾸기 (+{Sim.LargerFishNyang(goods.Id)}냥)", can, () => Sim.Donate(goods.Id), UiSfxEvent.Chest);
					}
					AddClaim(detail, wing);
					return detail;
				},
			};
		}

		private string StateLine(GoodsDef goods) => Sim.ExhibitStateOf(goods.Id) switch
		{
			ExhibitState.Donated => goods.Kind == GoodsKind.Fish ? $"받침대에 놓여 있습니다. {Sim.ExhibitSize(goods.Id)}cm" : "받침대에 놓여 있습니다.",
			ExhibitState.Held => "가지고 있습니다. 기증할 수 있습니다.",
			ExhibitState.Seen => "본 적은 있지만 지금 가진 것이 없습니다.",
			_ => "아직 모르는 것입니다.",
		};

		private void AddRecord(HomesteadDetail detail, GoodsDef goods)
		{
			if (goods.Kind == GoodsKind.Fish && Sim.State.FishRecords.TryGetValue(goods.Id, out FishRecord record))
				detail.Lines.Add($"낚은 수 {record.Count}, 가장 큰 것 {record.BestSize}cm");
			if (goods.Kind == GoodsKind.Relic && Sim.IsKnown(goods.Id))
				detail.Lines.Add($"{Catalog.Get<RelicDef>(goods.Id).Set} 벌");
		}

		private void AddWingProgress(HomesteadDetail detail, WingDef wing)
		{
			int count = Sim.DonatedCount(wing);
			int claimed = Sim.ClaimedMilestones(wing);
			if (claimed >= wing.Milestones.Count)
			{
				detail.Bar = 1f;
				detail.BarText = $"{wing.Name} 관 {count} / {Sim.ExhibitsOf(wing).Count}";
				return;
			}
			MilestoneDef next = wing.Milestones[claimed];
			detail.Bar = count / (float)next.Count;
			detail.BarText = $"{wing.Name} 관 {count} / {next.Count}";
			detail.Lines.Add($"{next.Count}점이 되면: {HomesteadText.Reward(next.Reward, Catalog)}");
		}

		private void AddClaim(HomesteadDetail detail, WingDef wing)
		{
			if (Sim.HasClaimableMilestone(wing))
				AddAction(detail, "보상 받기", HomesteadResult.Success, () => Sim.ClaimMilestone(wing.Id, out RewardDef _), UiSfxEvent.LevelUp);
		}

		// ---- 손질

		private HomesteadEntry RestoreEntry()
		{
			RelicRestoration restoring = Sim.State.Restoring;
			return new HomesteadEntry
			{
				Id = RESTORE_ID,
				Icon = Art.Icon("relic_dirty"),
				Name = restoring == null ? "흙 묻은 유물" : "손질 중",
				Sub = $"손질할 것 {Sim.State.DirtyRelics}",
				IsMarked = restoring != null || Sim.State.DirtyRelics > 0,
				Detail = RestoreDetail,
			};
		}

		private HomesteadDetail RestoreDetail()
		{
			RelicRestoration restoring = Sim.State.Restoring;
			HomesteadDetail detail = new() { Icon = Art.Icon("relic_dirty"), Title = "유물 손질", Note = "때에 맞는 도구를 고릅니다. 틀려도 망가지지 않습니다." };
			if (restoring == null)
			{
				detail.Lines.Add($"손질할 것 {Sim.State.DirtyRelics}");
				detail.Lines.Add("밭을 갈거나, 채집하거나, 낚거나, 갱도에서 나옵니다.");
				HomesteadResult can = Sim.IsOpen(HomesteadSim.MUSEUM) == false ? HomesteadResult.Fail(HomesteadFailure.NotBuilt, HomesteadSim.MUSEUM)
					: Sim.State.DirtyRelics > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods);
				AddAction(detail, "하나 꺼내기", can, Sim.BeginRestore);
				return detail;
			}
			RelicDef relic = Catalog.Get<RelicDef>(restoring.Relic);
			detail.Bar = restoring.Cleaned / (float)relic.Grime.Count;
			detail.BarText = $"{restoring.Cleaned} / {relic.Grime.Count}";
			int grime = relic.Grime[restoring.Cleaned];
			detail.Lines.Add(Sim.IsGrimeNamed() ? $"지금 자리: {HomesteadText.Grime(grime)}" : $"지금 자리: {HomesteadText.GrimeClue(grime)} (처음 보는 유물)");
			detail.Lines.Add(restoring.Mistakes == 0 ? $"한 번도 안 틀리면 {Catalog.Tuning.RelicPerfectNyang}냥 덤" : "덤은 다음 유물에서");
			for (int i = 0; i < HomesteadText.BrushCount; i++)
			{
				int tool = i;
				AddAction(detail, HomesteadText.Brush(tool), HomesteadResult.Success, () => Sim.Clean(tool, out bool _, out bool _));
			}
			return detail;
		}
	}
}
