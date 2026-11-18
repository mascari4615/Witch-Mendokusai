using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전투 시험 탭. 진열장 이동, 상태이상 걸기, 바닥 아이템, 레벨업과 상자, 무기 하나만, 무적, 시간 배율, 런 시드.
	/// 버튼는 전부 명령을 부름 (명령행에 친 것과 같은 경로, 콘솔에 결과).
	/// </summary>
	public sealed class TestBenchDevMode : IDevMode
	{
		public const string USS_ROW = "wm-dev-bench__row";
		public const string USS_HEADING = "wm-dev-bench__heading";
		public const string USS_STATE = "wm-dev-bench__state";

		public string Id => "bench";
		public string DisplayName => "시험";
		public VisualElement Root { get; } = new ScrollView();

		private readonly DevShowcase showcase;
		private readonly DevShowcaseSO data;
		private readonly Label stateLabel;
		private readonly DropdownField weaponField;
		private readonly List<EquipmentData> weapons = new();
		private IVisualElementScheduledItem refreshSchedule;

		// 스타일시트는 마운트하는 컨트롤러가 넘김
		public TestBenchDevMode(DevShowcase showcase, StyleSheet styleSheet)
		{
			this.showcase = showcase;
			data = showcase.Data;
			Root.styleSheets.Add(styleSheet);

			stateLabel = new Label();
			stateLabel.AddToClassList(USS_STATE);
			Root.Add(stateLabel);

			Row(Heading("이동"), Command("진열장으로", "showcase"));

			foreach (UnitStatusType type in UnitStatusRules.ALL)
			{
				string arg = type.ToString().ToLowerInvariant();
				Row(Heading(StatusName(type)), Command("가까운 적", "status", arg, "near"), Command("화면 전체", "status", arg, "screen"));
			}
			Row(Heading("전부"), Command("가까운 적", "status", "all", "near"), Command("화면 전체", "status", "all", "screen"));

			VisualElement dropRow = Row(Heading("바닥 아이템"));
			foreach (DevShowcaseDropCell drop in data.Drops)
				dropRow.Add(Command(drop.Label, "drop", drop.Type.ToString().ToLowerInvariant()));

			Row(Heading("판"), Command("레벨업 카드", "levelup"), Command("상자 열기", "chest"), Command("무적 켜기 끄기", "god"));

			weaponField = new DropdownField("무기 하나만");
			Row(weaponField, new Button(EquipSelected) { text = "장착" }, Command(data.BareHandsLabel, "weapon", "none"));

			VisualElement timeRow = Row(Heading("시간 배율"));
			foreach (float scale in data.TimeScalePresets)
			{
				string text = scale.ToString(CultureInfo.InvariantCulture);
				timeRow.Add(Command("x" + text, "timescale", text));
			}

			Row(Heading("런 시드"), Command("지금 값 고정", "seed", "fix"), Command("고정 해제", "seed", "off"));
		}

		public void OnActivate()
		{
			RefreshWeapons();
			Refresh();
			if (refreshSchedule == null)
				refreshSchedule = Root.schedule.Execute(Refresh).Every(data.TabRefreshMilliseconds);
			else
				refreshSchedule.Resume();
		}

		public void OnDeactivate() => refreshSchedule?.Pause();

		private void Refresh()
		{
			bool invincible = PlayerProvider.TryGetExistingInstance(out PlayerProvider players) && players.HasObject && players.CurrentObject.IsDevInvincible;
			float scale = TimeManager.TryGetExistingInstance(out TimeManager timeManager) ? timeManager.DebugScale : 1f;
			stateLabel.text = $"판 {(DungeonManagerBridge.IsDungeon ? "진행 중" : "밖")}, 무적 {(invincible ? "켬" : "끔")}, 시간 x{scale.ToString(CultureInfo.InvariantCulture)}\n{SeedCommand.Describe()}";
		}

		private void RefreshWeapons()
		{
			weapons.Clear();
			weapons.AddRange(showcase.BenchWeapons());
			List<string> names = new();
			foreach (EquipmentData weapon in weapons)
				names.Add($"{weapon.Name} (I_{weapon.ID})");
			weaponField.choices = names;
			if (weaponField.index < 0 && names.Count > 0)
				weaponField.index = 0;
		}

		private void EquipSelected()
		{
			if (weaponField.index < 0 || weaponField.index >= weapons.Count)
				return;
			DevWindowController.Instance.InvokeCommand("weapon", weapons[weaponField.index].ID.ToString());
		}

		// 한 칸짜리 상태이상 칸의 이름표. 없으면 열거 이름
		private string StatusName(UnitStatusType type)
		{
			foreach (DevShowcaseStatusCell cell in data.StatusCells)
				if (cell.Statuses.Count == 1 && cell.Statuses[0] == type)
					return cell.Label;
			return type.ToString();
		}

		private VisualElement Row(params VisualElement[] children)
		{
			VisualElement row = new();
			row.AddToClassList(USS_ROW);
			foreach (VisualElement child in children)
				row.Add(child);
			Root.Add(row);
			return row;
		}

		private static Label Heading(string text)
		{
			Label heading = new(text);
			heading.AddToClassList(USS_HEADING);
			return heading;
		}

		private static Button Command(string text, string command, params string[] args) =>
			new(() => DevWindowController.Instance.InvokeCommand(command, args)) { text = text };
	}
}
