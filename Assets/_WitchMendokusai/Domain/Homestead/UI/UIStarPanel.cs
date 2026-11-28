using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 별 보는 언덕 화면. 오늘 밤 별자리를 잇는 판 (StarBoard) 과 찾은 별자리 목록
	// - 낮이거나 오늘 이미 봤으면 판 없이 글만 (언제 다시 오면 되는지)
	// - 다 이으면 그 자리에서 별 조각과 오늘의 점을 받음. 닫으면 그만 (이은 선은 버려도 벌 없음)
	public sealed class UIStarPanel : UIToolkitPanel
	{
		private const string BLOCK = "wm-hs-star";

		private HomesteadManager manager;
		private StarLink link;
		private Label title;
		private Label message;
		private StarBoard board;
		private VisualElement atlas;

		public override string Name => "별 보기";

		private HomesteadSim Sim => manager.Sim;

		public void Open(HomesteadManager owner) => manager = owner;

		protected override void BuildUI(VisualElement content)
		{
			Root.styleSheets.Add(HomesteadArt.Instance.StyleSheet);
			VisualElement shade = new() { pickingMode = PickingMode.Ignore };
			shade.AddToClassList(BLOCK + "__shade");
			AddBackdrop(shade);

			VisualElement column = new();
			column.AddToClassList(BLOCK);
			content.Add(column);

			title = new Label();
			title.AddToClassList(BLOCK + "__title");
			column.Add(title);
			message = new Label();
			message.AddToClassList(BLOCK + "__message");
			column.Add(message);

			VisualElement row = new();
			row.AddToClassList(BLOCK + "__row");
			column.Add(row);
			board = new StarBoard();
			board.Pressed += Press;
			row.Add(board);
			atlas = new VisualElement();
			atlas.AddToClassList(BLOCK + "__atlas");
			row.Add(atlas);

			Button close = new(() => PanelGroup.ClosePanel()) { focusable = false, text = "닫기" };
			close.ClearClassList();
			close.AddToClassList(BLOCK + "__close");
			column.Add(close);
		}

		protected override void OnOpen()
		{
			MobileControlsView.Hold(this, true);
			link = null;
			HomesteadResult can = Sim.CanStargaze(manager.Hour, manager.CalendarDay);
			if (can.Ok)
			{
				link = Sim.StartStarLink(manager.Season, manager.CalendarDay);
				board.Show(link);
			}
			board.style.display = link != null ? DisplayStyle.Flex : DisplayStyle.None;
			title.text = link != null ? $"오늘 밤 하늘: {NameOf(link.Constellation)}" : "별 보는 언덕";
			message.text = link != null ? "별을 눌러 흐린 선을 따라 이으세요." : ClosedText(can);
			RefreshAtlas();
		}

		protected override void OnClose() => MobileControlsView.Hold(this, false);

		private void OnDestroy() => MobileControlsView.Hold(this, false);

		private string NameOf(ConstellationDef constellation) => Sim.IsConstellationFound(constellation.Id) ? constellation.Name : "???";

		private string ClosedText(HomesteadResult can)
		{
			StarTuning tuning = Sim.Catalog.StarTuning;
			if (can.Failure == HomesteadFailure.AlreadyDone)
				return Sim.HasOmen ? $"오늘 밤 하늘은 이미 봤습니다. {OmenLine(Sim.OmenConstellation)}" : "오늘 밤 하늘은 이미 봤습니다. 내일 밤에 또 오세요.";
			return $"별은 밤에 보입니다 ({tuning.FromHour}시부터 {tuning.ToHour}시 전). 지금 {manager.Hour}시";
		}

		private static string OmenLine(ConstellationDef omen) => $"오늘의 점: {omen.Saying}. 다음 던전 한 판 {HomesteadText.OmenText(omen.Omen, omen.Value)}";

		private void Press(int star)
		{
			if (link == null || link.IsDone)
				return;
			int line = link.Press(star);
			board.Refresh();
			if (line < 0)
				return;
			if (link.IsDone == false)
			{
				UiSfx.Play(UiSfxEvent.Confirm);
				return;
			}
			Finish();
		}

		private void Finish()
		{
			ConstellationDef found = link.Constellation;
			int shards = Sim.ShardsFor(found.Id);
			bool first = Sim.IsConstellationFound(found.Id) == false;
			HomesteadResult result = Sim.FinishStargaze(link, manager.Hour, manager.CalendarDay);
			if (result.Ok == false)
			{
				UiSfx.Play(UiSfxEvent.PurchaseFail);
				message.text = "그새 날이 밝았습니다. 오늘 밤에 다시 이어 보세요.";
				return;
			}
			manager.Save();
			UiSfx.Play(UiSfxEvent.LevelUp);
			string shardName = Sim.Catalog.Get<GoodsDef>(Sim.Catalog.StarTuning.Shard).Name;
			title.text = $"{found.Name} 자리{(first ? " (처음 찾음)" : string.Empty)}";
			message.text = $"{shardName} +{shards}. {OmenLine(found)}";
			RefreshAtlas();
		}

		// 계절마다 한 줄. 찾은 별자리는 이름, 못 찾은 것은 흐린 ???
		private void RefreshAtlas()
		{
			atlas.Clear();
			Label head = new($"별자리 {Sim.State.Constellations.Count} / {Sim.Catalog.Constellations.Count}");
			head.AddToClassList(BLOCK + "__atlas-head");
			atlas.Add(head);
			foreach (ConstellationDef constellation in Sim.Catalog.Constellations)
			{
				bool found = Sim.IsConstellationFound(constellation.Id);
				Label entry = new($"{HomesteadText.Season(constellation.Season)}  {(found ? constellation.Name : "???")}");
				entry.AddToClassList(BLOCK + "__atlas-entry");
				entry.EnableInClassList(BLOCK + "__atlas-entry--unknown", found == false);
				entry.EnableInClassList(BLOCK + "__atlas-entry--tonight", link != null && link.Constellation == constellation);
				atlas.Add(entry);
			}
		}

		public override void UpdateUI()
		{
		}
	}
}
