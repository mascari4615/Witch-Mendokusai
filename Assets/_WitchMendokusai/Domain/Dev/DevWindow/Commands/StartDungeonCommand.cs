using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// dungeon &lt;stageRef&gt;
	/// stageRef: ID 정수 / `D_&lt;num&gt;` / Name 부분.
	/// DungeonManager.StartDungeon 호출.
	/// </summary>
	public class StartDungeonCommand : IDevCommand
	{
		public string Name => "dungeon";
		public string Usage => "dungeon <stageRef>";

		private const string CODE_PREFIX = "D_";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}

			Dungeon target = DevDataLookup.Resolve<Dungeon>(args[0], CODE_PREFIX, out List<Dungeon> matches);
			if (target == null)
			{
				if (matches.Count == 0)
				{
					context.LogError($"매칭되는 던전 없음: '{args[0]}'");
					return;
				}
				context.LogWarn($"여러 후보 ({matches.Count}) — 더 정확히 입력:");
				for (int i = 0; i < matches.Count && i < 10; i++)
					context.LogWarn($"  {CODE_PREFIX}{matches[i].ID}  {matches[i].Name}");
				if (matches.Count > 10)
					context.LogWarn($"  ... 외 {matches.Count - 10}개");
				return;
			}

			DungeonManager.Instance.StartDungeon(target);
			context.LogSuccess($"{target.Name} 시작 (ID {target.ID})");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return System.Linq.Enumerable.Empty<string>();
			return DevDataLookup.SuggestRefs<Dungeon>(partial[0], CODE_PREFIX);
		}
	}
}
