using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 연구대. 생활 주문 목록과 연구 (한 번만). 쓰는 곳과 쓰는 값을 같이 보임
	public sealed class SpellPage : HomesteadPage
	{
		private const string TAB_SPELL = "주문";
		private const string ICON_KEY = "magic_desk";
		private const string KNOWN_ICON_KEY = "book_pile";

		private static readonly string[] TABS = { TAB_SPELL };

		public SpellPage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "연구대";
		protected override string IconKey => ICON_KEY;
		public override IReadOnlyList<string> Tabs => TABS;
		public override string Status => $"배운 주문 {Sim.State.Spells.Count} / {Catalog.Spells.Count}";

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			foreach (SpellDef spell in Catalog.Spells)
				entries.Add(SpellEntry(spell));
			return entries;
		}

		// 연구는 세이브에 바로 남김 (재료가 빠진 뒤라)
		private HomesteadResult Saved(HomesteadResult result)
		{
			if (result.Ok)
				Manager.Save();
			return result;
		}

		private HomesteadEntry SpellEntry(SpellDef spell)
		{
			bool known = Sim.IsSpellKnown(spell.Id);
			string iconKey = known ? KNOWN_ICON_KEY : ICON_KEY;
			return new HomesteadEntry
			{
				Id = spell.Id,
				Icon = Art.Icon(iconKey),
				Name = spell.Name,
				Sub = known ? "배움" : "연구 전",
				IsDim = known == false,
				IsMarked = known == false && Sim.CanResearch(spell.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(iconKey), Title = spell.Name, Note = spell.Note };
					if (known)
					{
						detail.Lines.Add("쓸 때마다");
						foreach (KeyValuePair<string, int> pair in spell.Cast)
							AddGoods(detail, pair.Key, pair.Value);
						return detail;
					}
					detail.Lines.Add("연구에 한 번");
					foreach (KeyValuePair<string, int> pair in spell.Research)
						AddGoods(detail, pair.Key, pair.Value);
					detail.Lines.Add("배운 뒤 쓸 때마다");
					foreach (KeyValuePair<string, int> pair in spell.Cast)
						AddGoods(detail, pair.Key, pair.Value);
					AddAction(detail, "연구하기", Sim.CanResearch(spell.Id), () => Saved(Sim.Research(spell.Id)), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}
	}
}
