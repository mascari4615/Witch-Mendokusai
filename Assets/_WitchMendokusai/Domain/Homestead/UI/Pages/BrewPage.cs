using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 작업방 솥. 물약 고르기와 물약 선반 한 칸
	// - 솥: 물약을 골라 솥 지도 (HomeRoom.Brew) 로. 처방 노트에 있으면 다시 만들기 한 번
	// - 선반: 가진 물약 하나를 올리면 다음 던전 한 판에 마심. 식탁과 같이 걸림
	public sealed class BrewPage : HomesteadPage
	{
		private const string TAB_BREW = "솥";
		private const string TAB_SHELF = "선반";
		private const string SHELF_ID = "shelf";
		private const string ICON_KEY = "cauldron";
		private const string SHELF_ICON_KEY = "potion_shelf";

		private static readonly string[] TABS = { TAB_BREW, TAB_SHELF };

		public BrewPage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "솥";
		protected override string IconKey => ICON_KEY;
		public override IReadOnlyList<string> Tabs => TABS;

		// 방금 솥에서 나온 것이 먼저. 없으면 선반
		public override string Status
		{
			get
			{
				string shelf = Sim.HasPotion ? $"선반: {Catalog.Get<GoodsDef>(Sim.State.Potion).Name}" : "선반이 비어 있음";
				return Manager.BrewNews.Length > 0 ? $"{Manager.BrewNews}. {shelf}" : shelf;
			}
		}

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			if (Tabs[tab] == TAB_BREW)
			{
				foreach (BrewDef brew in Catalog.Brews)
				{
					if (brew.Fallback == false)
						entries.Add(BrewEntry(brew));
				}
				return entries;
			}
			entries.Add(ShelfEntry());
			foreach (BrewDef brew in Catalog.Brews)
			{
				if (Sim.Count(brew.Output) > 0)
					entries.Add(PotionEntry(brew));
			}
			return entries;
		}

		private static string EffectText(BrewDef brew) => $"다음 던전 한 판 {HomesteadText.PotionEffectText(brew.Effect, brew.Value)}";

		private HomesteadResult Saved(HomesteadResult result)
		{
			if (result.Ok)
				Manager.Save();
			return result;
		}

		private HomesteadResult OpenMap(BrewDef brew)
		{
			Manager.BrewNews = string.Empty;
			Manager.RequestBrew(brew.Id);
			return HomesteadResult.Success;
		}

		private HomesteadEntry BrewEntry(BrewDef brew)
		{
			GoodsDef output = Catalog.Get<GoodsDef>(brew.Output);
			bool known = Sim.IsBrewKnown(brew.Id);
			return new HomesteadEntry
			{
				Id = brew.Id,
				Icon = Art.IconOf(output),
				Name = known ? brew.Name : "???",
				Sub = known ? EffectText(brew) : "솥 지도에서 초록 원을 찾아 보세요",
				Count = Sim.Count(brew.Output).ToString(),
				IsUnknown = known == false,
				IsMarked = Sim.CanRebrew(brew.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(output), Title = known ? brew.Name : "???", Note = known ? EffectText(brew) : "아직 모르는 물약" };
					if (known && Sim.TryGetNote(brew.Id, out BrewNote note))
					{
						detail.Lines.Add($"처방 노트: 한 번에 {note.Amount}개");
						foreach (KeyValuePair<string, int> pair in note.Inputs)
							AddGoods(detail, pair.Key, pair.Value);
						AddAction(detail, "노트대로 만들기", Sim.CanRebrew(brew.Id), () => Saved(Sim.Rebrew(brew.Id)), UiSfxEvent.Purchase);
					}
					else
					{
						detail.Lines.Add("재료를 하나씩 넣어 지도 위 초록 원에 닿게 하세요. 못 닿아도 흐린 물약이 나옵니다.");
					}
					AddAction(detail, "솥 지도 열기", HomesteadResult.Success, () => OpenMap(brew));
					return detail;
				},
			};
		}

		private HomesteadEntry ShelfEntry()
		{
			bool has = Sim.HasPotion;
			GoodsDef potion = has ? Catalog.Get<GoodsDef>(Sim.State.Potion) : null;
			return new HomesteadEntry
			{
				Id = SHELF_ID,
				Icon = has ? Art.IconOf(potion) : Art.Icon(SHELF_ICON_KEY),
				Name = "물약 선반",
				Sub = has ? potion.Name : "비어 있음",
				IsDim = has == false,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = has ? Art.IconOf(potion) : Art.Icon(SHELF_ICON_KEY), Title = "물약 선반", Note = "던전에 가기 전에 마시고 갑니다." };
					if (has == false)
					{
						detail.Lines.Add(HasAnyPotion() ? "아래 물약 중 하나를 올리세요." : "가진 물약이 없습니다. 솥 탭에서 만드세요.");
						return detail;
					}
					Sim.TryGetPotion(potion.Id, out BrewDef brew);
					detail.Lines.Add($"{potion.Name}: {EffectText(brew)}");
					AddAction(detail, "내리기", HomesteadResult.Success, () => Saved(Sim.ClearPotion()));
					return detail;
				},
			};
		}

		private bool HasAnyPotion() => Catalog.Brews.Exists(brew => Sim.Count(brew.Output) > 0);

		private HomesteadEntry PotionEntry(BrewDef brew)
		{
			GoodsDef output = Catalog.Get<GoodsDef>(brew.Output);
			bool onShelf = Sim.State.Potion == brew.Output;
			return new HomesteadEntry
			{
				Id = brew.Output,
				Icon = Art.IconOf(output),
				Name = output.Name,
				Sub = EffectText(brew),
				Count = Sim.Count(brew.Output).ToString(),
				IsMarked = onShelf,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(output), Title = output.Name, Note = EffectText(brew) };
					detail.Lines.Add(onShelf ? "선반에 있음" : $"가진 것 {Sim.Count(brew.Output)}");
					AddAction(detail, Sim.HasPotion ? "바꿔 올리기" : "선반에 올리기", Sim.CanSetPotion(brew.Output), () => Saved(Sim.SetPotion(brew.Output)));
					return detail;
				},
			};
		}
	}
}
