using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 화덕. 요리법 고르기와 식탁 한 칸
	// - 요리: 재료를 보고 굽기를 누르면 바늘 화면 (HomeRoom.Cook) 으로
	// - 식탁: 가진 음식 하나를 올리면 다음 던전 한 판에 먹음
	public sealed class CookPage : HomesteadPage
	{
		private const string TAB_COOK = "요리";
		private const string TAB_TABLE = "식탁";
		private const string TABLE_ID = "table";
		private const string ICON_KEY = "stew";

		private static readonly string[] TABS = { TAB_COOK, TAB_TABLE };

		public CookPage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "부엌";
		protected override string IconKey => ICON_KEY;
		public override IReadOnlyList<string> Tabs => TABS;
		public override string Status => Sim.HasMeal ? $"식탁: {Catalog.Get<GoodsDef>(Sim.State.Meal).Name}" : "식탁이 비어 있음";

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			if (Tabs[tab] == TAB_COOK)
			{
				foreach (CookDef cook in Catalog.Cooks)
					entries.Add(CookEntry(cook));
				return entries;
			}
			entries.Add(TableEntry());
			foreach (CookDef cook in Catalog.Cooks)
			{
				if (Sim.Count(cook.Output) > 0)
					entries.Add(MealEntry(cook));
			}
			return entries;
		}

		private static string BuffText(CookDef cook) => $"다음 던전 한 판 {HomesteadText.MealBuffText(cook.Buff, cook.Value)}";

		// 식탁은 세이브에 바로 남김 (굽기와 같은 결)
		private HomesteadResult Saved(HomesteadResult result)
		{
			if (result.Ok)
				Manager.Save();
			return result;
		}

		// 조사를 피해 이름을 끝에 둠 (받침 따라 을, 를이 갈림)
		private string UnlockText(CookDef cook) => $"{Catalog.Get<GoodsDef>(cook.UnlockBy).Name}: 처음 얻으면 열림";

		private HomesteadEntry CookEntry(CookDef cook)
		{
			GoodsDef output = Catalog.Get<GoodsDef>(cook.Output);
			bool open = Sim.IsCookOpen(cook);
			return new HomesteadEntry
			{
				Id = cook.Id,
				Icon = Art.IconOf(output),
				Name = open ? cook.Name : "???",
				Sub = open ? BuffText(cook) : UnlockText(cook),
				Count = Sim.Count(cook.Output).ToString(),
				IsDim = open == false,
				IsUnknown = open == false,
				IsMarked = Sim.CanCook(cook.Id).Ok,
				Detail = () =>
				{
					if (open == false)
					{
						HomesteadDetail locked = new() { Icon = Art.IconOf(output), Title = "???", Note = "아직 모르는 요리" };
						locked.Lines.Add(UnlockText(cook));
						return locked;
					}
					HomesteadDetail detail = new() { Icon = Art.IconOf(output), Title = cook.Name, Note = BuffText(cook) };
					detail.Lines.Add($"가진 것 {Sim.Count(cook.Output)}. 바늘이 초록 칸일 때 누르면 하나 더");
					foreach (KeyValuePair<string, int> pair in cook.Inputs)
						AddGoods(detail, pair.Key, pair.Value);
					AddAction(detail, "요리하기", Sim.CanCook(cook.Id), () => Done(() => Manager.RequestCook(cook.Id)));
					return detail;
				},
			};
		}

		private HomesteadEntry TableEntry()
		{
			bool has = Sim.HasMeal;
			GoodsDef meal = has ? Catalog.Get<GoodsDef>(Sim.State.Meal) : null;
			return new HomesteadEntry
			{
				Id = TABLE_ID,
				Icon = has ? Art.IconOf(meal) : Art.Icon(ICON_KEY),
				Name = "식탁",
				Sub = has ? meal.Name : "비어 있음",
				IsDim = has == false,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = has ? Art.IconOf(meal) : Art.Icon(ICON_KEY), Title = "식탁", Note = "던전에 가기 전에 먹고 갑니다." };
					if (has == false)
					{
						detail.Lines.Add(HasAnyDish() ? "아래 음식 중 하나를 올리세요." : "가진 음식이 없습니다. 요리 탭에서 만드세요.");
						return detail;
					}
					Sim.TryGetMeal(meal.Id, out CookDef cook);
					detail.Lines.Add($"{meal.Name}: {BuffText(cook)}");
					AddAction(detail, "내리기", HomesteadResult.Success, () => Saved(Sim.ClearMeal()));
					return detail;
				},
			};
		}

		private bool HasAnyDish() => Catalog.Cooks.Exists(cook => Sim.Count(cook.Output) > 0);

		private HomesteadEntry MealEntry(CookDef cook)
		{
			GoodsDef output = Catalog.Get<GoodsDef>(cook.Output);
			bool onTable = Sim.State.Meal == cook.Output;
			return new HomesteadEntry
			{
				Id = cook.Output,
				Icon = Art.IconOf(output),
				Name = output.Name,
				Sub = BuffText(cook),
				Count = Sim.Count(cook.Output).ToString(),
				IsMarked = onTable,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(output), Title = output.Name, Note = BuffText(cook) };
					detail.Lines.Add(onTable ? "식탁에 있음" : $"가진 것 {Sim.Count(cook.Output)}");
					AddAction(detail, Sim.HasMeal ? "바꿔 올리기" : "식탁에 올리기", Sim.CanSetMeal(cook.Output), () => Saved(Sim.SetMeal(cook.Output)));
					return detail;
				},
			};
		}
	}
}
