using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 마을에서 떠 있는 것. 얻은 것 줄, 큰 알림, 귀환 소식, 낚시와 갱도의 눈금
	public sealed class HomesteadHud : MonoBehaviour
	{
		private const string BLOCK = "wm-hs-hud";

		[SerializeField] private int feedLimit = 6;
		[SerializeField] private int feedMilliseconds = 2600;
		[SerializeField] private int newFeedMilliseconds = 4200;
		[SerializeField] private int fadeMilliseconds = 300;
		[SerializeField] private int noticeMilliseconds = 2600;
		[SerializeField] private int digestMilliseconds = 9000;

		private HomesteadManager manager;
		private VisualElement root;
		private VisualElement feed;
		private VisualElement notices;
		private VisualElement digest;
		private VisualElement goal;
		private bool goalShown;
		private VisualElement duel;
		private VisualElement duelIcon;
		private VisualElement tensionFill;
		private VisualElement sweetZone;
		private VisualElement progressFill;
		private Label duelHint;
		private VisualElement mine;
		private Label mineDepth;
		private VisualElement lanternFill;
		private Label lanternText;
		private Label mineHint;

		private HomesteadSim Sim => manager.Sim;
		private HomesteadArt Art => HomesteadArt.Instance;

		public void Build(UIRoot uiRoot, HomesteadManager owner)
		{
			manager = owner;
			root = new VisualElement { name = "HomesteadHud", pickingMode = PickingMode.Ignore };
			root.AddToClassList(BLOCK);
			root.styleSheets.Add(Art.StyleSheet);
			uiRoot.ModeHudLayer.Add(root);

			feed = Make("feed", root);
			notices = Make("notices", root);
			digest = Make("digest", root);
			goal = Make("goal", root);
			ShowGoal(false);
			BuildDuel();
			BuildMine();
			ShowDuel(false);
			ShowMine(false);

			Sim.Changed += RefreshGoal;
			Sim.Gained += OnGained;
			Sim.RelicFound += OnRelicFound;
			Sim.Arrived += OnArrived;
			Sim.AffinityStepped += OnAffinityStepped;
			Sim.GuildLeveled += OnGuildLeveled;
		}

		private void OnDestroy()
		{
			root?.RemoveFromHierarchy();
			if (manager == null || manager.Sim == null)
				return;
			Sim.Changed -= RefreshGoal;
			Sim.Gained -= OnGained;
			Sim.RelicFound -= OnRelicFound;
			Sim.Arrived -= OnArrived;
			Sim.AffinityStepped -= OnAffinityStepped;
			Sim.GuildLeveled -= OnGuildLeveled;
		}

		private static VisualElement Make(string element, VisualElement parent)
		{
			VisualElement made = new() { pickingMode = PickingMode.Ignore };
			made.AddToClassList($"{BLOCK}__{element}");
			parent.Add(made);
			return made;
		}

		private static Label MakeLabel(string element, VisualElement parent, string text = "")
		{
			Label label = new(text) { pickingMode = PickingMode.Ignore };
			label.AddToClassList($"{BLOCK}__{element}");
			parent.Add(label);
			return label;
		}

		// ---- 건설

		private VillageBuildHud build;

		public VillageBuildHud ShowBuild(IReadOnlyList<PlaceableDef> palette, System.Action<PlaceableDef> onTake, System.Action onUndo, System.Action onLeave)
		{
			HideBuild();
			build = new VillageBuildHud(root, palette, onTake, onUndo, onLeave);
			ShowGoal(false);
			return build;
		}

		public void HideBuild()
		{
			if (build == null)
				return;
			build.Remove();
			build = null;
			ShowGoal(true);
		}

		// ---- 얻은 것

		private readonly Dictionary<string, (Label Count, int Amount, VisualElement Row)> shownRows = new();

		private void OnGained(HomesteadGain gain)
		{
			GoodsDef goods = Sim.Catalog.Get<GoodsDef>(gain.GoodsId);
			if (gain.IsNew)
				UiSfx.Play(UiSfxEvent.CardSelect);
			// 같은 물건이 이어서 들어오면 줄을 늘리지 않고 수만
			if (shownRows.TryGetValue(gain.GoodsId, out (Label Count, int Amount, VisualElement Row) shown) && shown.Row.panel != null)
			{
				int total = shown.Amount + gain.Amount;
				shown.Count.text = $"+{total}";
				shownRows[gain.GoodsId] = (shown.Count, total, shown.Row);
				return;
			}
			VisualElement row = Make("feed-row", feed);
			row.EnableInClassList($"{BLOCK}__feed-row--new", gain.IsNew);
			VisualElement icon = Make("feed-icon", row);
			icon.style.backgroundImage = new StyleBackground(Art.IconOf(goods));
			MakeLabel("feed-name", row, goods.Name);
			if (gain.IsNew)
				MakeLabel("feed-new", row, "처음");
			Label count = MakeLabel("feed-count", row, $"+{gain.Amount}");
			shownRows[gain.GoodsId] = (count, gain.Amount, row);
			while (feed.childCount > feedLimit)
				feed.RemoveAt(0);
			Retire(row, gain.IsNew ? newFeedMilliseconds : feedMilliseconds, () => shownRows.Remove(gain.GoodsId));
		}

		private void Retire(VisualElement element, int milliseconds, System.Action onGone = null)
		{
			element.schedule.Execute(() => element.AddToClassList($"{BLOCK}--out")).ExecuteLater(milliseconds);
			element.schedule.Execute(() =>
			{
				element.RemoveFromHierarchy();
				onGone?.Invoke();
			}).ExecuteLater(milliseconds + fadeMilliseconds);
		}

		// ---- 큰 알림

		public void Notice(string title, string text, Sprite icon)
		{
			VisualElement card = Make("notice", notices);
			VisualElement image = Make("notice-icon", card);
			image.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
			VisualElement texts = Make("notice-texts", card);
			MakeLabel("notice-title", texts, title);
			MakeLabel("notice-text", texts, text);
			Retire(card, noticeMilliseconds);
		}

		private void OnRelicFound()
		{
			UiSfx.Play(UiSfxEvent.Chest);
			Notice("흙 묻은 유물", "박물관에서 손질할 수 있습니다.", Art.Icon("relic_dirty"));
		}

		private void OnArrived(string residentId)
		{
			ResidentDef resident = Sim.Catalog.Get<ResidentDef>(residentId);
			UiSfx.Play(UiSfxEvent.LevelUp);
			Notice($"{resident.Name}이 왔습니다", $"{Sim.Catalog.Get<BuildingDef>(resident.Building).Name}을 맡습니다.", Art.Icon(residentId));
		}

		private void OnAffinityStepped(string residentId, int step)
		{
			ResidentDef resident = Sim.Catalog.Get<ResidentDef>(residentId);
			Notice($"{resident.Name}  호감 {step}단", HomesteadText.Reward(resident.AffinityRewards[step - 1], Sim.Catalog), Art.Icon(residentId));
		}

		private void OnGuildLeveled(int level)
		{
			UiSfx.Play(UiSfxEvent.LevelUp);
			Notice($"길드 레벨 {level}", "새 의뢰와 사람이 찾아옵니다.", Art.Icon("board"));
		}

		// ---- 귀환 소식

		public void ShowDigest(HomesteadDigest news)
		{
			digest.Clear();
			digest.EnableInClassList($"{BLOCK}--none", news.IsEmpty);
			if (news.IsEmpty)
				return;
			VisualElement card = Make("digest-card", digest);
			MakeLabel("digest-title", card, "그동안 마을에서는");
			foreach (string residentId in news.NewResidents)
				AddNews(card, residentId, $"{Sim.Catalog.Get<ResidentDef>(residentId).Name}이 새로 왔습니다");
			foreach (string building in news.DoneWorks)
				AddNews(card, building, $"{Sim.Catalog.Get<BuildingDef>(building).Name} 공사가 끝났습니다");
			AddNews(card, HomesteadSim.FARM, news.RipePlots, "밭 {0}칸이 익었습니다");
			AddNews(card, HomesteadSim.WORKSHOP, news.DoneCrafts, "공작소에 다 된 것 {0}개");
			AddNews(card, "dock", news.JobYields, "맡긴 일에서 거둘 것 {0}개");
			AddNews(card, HomesteadSim.RANCH, news.AnimalProducts, "동물이 낸 것 {0}개");
			AddNews(card, "board", news.DeliverableRequests, "건넬 수 있는 의뢰 {0}건");
			AddNews(card, "relic_dirty", news.DirtyRelics, "손질할 유물 {0}점");
			Retire(card, digestMilliseconds);
		}

		private void AddNews(VisualElement card, string iconKey, int count, string format)
		{
			if (count > 0)
				AddNews(card, iconKey, string.Format(format, count));
		}

		private void AddNews(VisualElement card, string iconKey, string text)
		{
			VisualElement row = Make("digest-row", card);
			Make("digest-icon", row).style.backgroundImage = new StyleBackground(Art.Icon(iconKey));
			MakeLabel("digest-text", row, text);
		}

		// ---- 다음 회관까지. 마을에 있는 동안 늘 보임

		public void ShowGoal(bool visible)
		{
			goalShown = visible;
			goal.EnableInClassList($"{BLOCK}--none", visible == false);
			goal.userData = null;
			RefreshGoal();
		}

		private void RefreshGoal()
		{
			if (goalShown == false)
				return;
			string key = GoalKey();
			if (goal.userData as string == key)
				return;
			goal.userData = key;
			goal.Clear();
			bool hasNext = Sim.TryGetNextTownHall(out TownHallDef next);
			goal.EnableInClassList($"{BLOCK}--none", hasNext == false);
			if (hasNext == false)
				return;
			MakeLabel("goal-title", goal, next.Name);
			foreach (KeyValuePair<string, int> pair in next.Cost.Goods)
				AddGoal(Art.IconOf(Sim.Catalog.Get<GoodsDef>(pair.Key)), Sim.Count(pair.Key), pair.Value);
			if (next.Cost.Nyang > 0)
				AddGoal(Art.Icon("nyang"), Sim.Purse.Nyang, next.Cost.Nyang);
		}

		private string GoalKey()
		{
			if (Sim.TryGetNextTownHall(out TownHallDef next) == false)
				return "max";
			System.Text.StringBuilder key = new(next.Name);
			foreach (KeyValuePair<string, int> pair in next.Cost.Goods)
				key.Append(';').Append(Sim.Count(pair.Key));
			return key.Append(';').Append(Sim.Purse.Nyang >= next.Cost.Nyang).Append(Sim.Purse.Nyang).ToString();
		}

		private void AddGoal(Sprite icon, int have, int need)
		{
			VisualElement row = Make("goal-row", goal);
			Make("goal-icon", row).style.backgroundImage = new StyleBackground(icon);
			Label value = MakeLabel("goal-value", row, $"{Mathf.Min(have, need)} / {need}");
			value.EnableInClassList($"{BLOCK}__goal-value--done", have >= need);
		}

		// ---- 낚시

		private void BuildDuel()
		{
			duel = Make("duel", root);
			duelHint = MakeLabel("duel-hint", duel);
			VisualElement progress = Make("duel-progress", duel);
			progressFill = Make("duel-progress-fill", progress);
			duelIcon = Make("duel-icon", progress);
			VisualElement tension = Make("duel-tension", duel);
			sweetZone = Make("duel-sweet", tension);
			tensionFill = Make("duel-tension-fill", tension);
		}

		public void ShowDuel(bool visible) => duel.EnableInClassList($"{BLOCK}--none", visible == false);

		public void SetDuel(FishingDuel state)
		{
			bool inDuel = state.Phase == FishingPhase.Duel;
			progressFill.parent.EnableInClassList($"{BLOCK}--none", inDuel == false);
			tensionFill.parent.EnableInClassList($"{BLOCK}--none", inDuel == false);
			duelHint.text = DuelHint(state);
			duelHint.EnableInClassList($"{BLOCK}__duel-hint--bite", state.Phase == FishingPhase.Bite);
			if (inDuel == false)
				return;
			HomesteadTuning tuning = Sim.Catalog.Tuning;
			sweetZone.style.left = Length.Percent(tuning.SweetFrom * 100f);
			sweetZone.style.width = Length.Percent((tuning.SweetTo - tuning.SweetFrom) * 100f);
			tensionFill.style.width = Length.Percent(state.Tension * 100f);
			tensionFill.EnableInClassList($"{BLOCK}__duel-tension-fill--sweet", state.IsSweet);
			tensionFill.EnableInClassList($"{BLOCK}__duel-tension-fill--danger", state.Tension > tuning.SweetTo);
			progressFill.style.width = Length.Percent(state.Progress * 100f);
			duelIcon.style.left = Length.Percent(state.Progress * 100f);
			duelIcon.style.backgroundImage = new StyleBackground(Art.Icon("fish_shadow"));
			duel.EnableInClassList($"{BLOCK}__duel--surge", state.IsSurging || state.IsSurgeNear);
		}

		private static string DuelHint(FishingDuel state)
		{
			switch (state.Phase)
			{
				case FishingPhase.Waiting:
					return "기다리는 중";
				case FishingPhase.Bite:
					return "지금!";
				default:
					return state.IsSurging ? "몸부림. 놓아요" : state.IsSurgeNear ? "곧 몸부림" : "누르고 있으면 감깁니다";
			}
		}

		// ---- 갱도

		private void BuildMine()
		{
			mine = Make("mine", root);
			mineDepth = MakeLabel("mine-depth", mine);
			VisualElement lantern = Make("mine-lantern", mine);
			lanternFill = Make("mine-lantern-fill", lantern);
			lanternText = MakeLabel("mine-lantern-text", lantern);
			mineHint = MakeLabel("mine-hint", mine);
		}

		public void ShowMine(bool visible) => mine.EnableInClassList($"{BLOCK}--none", visible == false);

		public void SetMine(MineRun run)
		{
			mineDepth.text = $"{run.Depth}층  {run.Band.Name}";
			lanternFill.style.width = Length.Percent(run.Lantern * 100f / run.LanternMax);
			lanternText.text = $"등불 {run.Lantern}";
			mineHint.text = run.PassageOpen ? "내리막이 열렸습니다" : run.Lantern == 0 ? "등불이 다했습니다. 승강기로" : "바위 아래 내리막이 숨어 있습니다";
		}
	}
}
