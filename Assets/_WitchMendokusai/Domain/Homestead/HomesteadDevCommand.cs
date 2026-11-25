using System.Collections.Generic;
using System.Linq;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 생활 갈래 확인용. homestead go, homestead rich <개수>, homestead time <분>, homestead built, homestead guild <경험>, homestead furniture, homestead rest, homestead grid
	public sealed class HomesteadDevCommand : IDevCommand
	{
		private const int STAGE_ID = 6;
		private const string GO = "go";
		private const string RICH = "rich";
		private const string TIME = "time";
		private const string BUILT = "built";
		private const string GUILD = "guild";
		private const string FURNITURE = "furniture";
		private const string REST = "rest";
		private const string GRID = "grid";
		private static readonly string[] ARGS = { GO, RICH, TIME, BUILT, GUILD, FURNITURE, REST, GRID };

		public string Name => "homestead";
		public string Usage => "homestead go|rich <amount>|time <minutes>|built|guild <exp>|furniture|rest|grid";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length == 0)
			{
				context.LogError(Usage);
				return;
			}
			HomesteadManager manager = HomesteadManager.Instance;
			HomesteadSim sim = manager.Sim;
			int number = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 0;
			switch (args[0])
			{
				case GO:
					StageManager.Instance.LoadStage(SOHelper.Get<WorldStage>(STAGE_ID));
					break;
				case RICH:
					sim.DevFill(number, number * 100);
					break;
				case TIME:
					sim.AdvanceMinutes(number);
					break;
				case BUILT:
					sim.DevBuildAll();
					break;
				case GUILD:
					sim.DevAddGuildExp(number);
					break;
				case FURNITURE:
					sim.DevGiveFurniture();
					break;
				case REST:
					sim.Rest(-sim.Day - 2, sim.MinutesUntilMorning(manager.Hour, manager.Minute));
					break;
				case GRID:
					UnityEngine.Object.FindAnyObjectByType<VillageStage>().ToggleGrid();
					break;
				default:
					context.LogError(Usage);
					return;
			}
			context.LogSuccess($"회관 {sim.State.TownHall}, 길드 {sim.GuildLevel}, 기증 {sim.State.Donated.Count}");
		}

		public IEnumerable<string> Suggest(string[] partial) =>
			partial.Length == 1 ? DevCommandArgs.StartsWith(ARGS, partial[0]) : Enumerable.Empty<string>();
	}
}
