using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// help [commandName]
	/// - 인자 없음: 등록된 모든 명령의 Usage 나열
	/// - 인자 1개: 해당 명령의 Usage 만 출력
	/// </summary>
	public class HelpCommand : IDevCommand
	{
		public string Name => "help";
		public string Usage => "help [commandName]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length == 0)
			{
				context.LogInfo("─ 등록된 명령 ─");
				foreach (IDevCommand command in DevCommandRegistry.Instance.AllCommands.OrderBy(command => command.Name))
					context.LogInfo($"  {command.Usage}");
				return;
			}

			string target = args[0];
			if (DevCommandRegistry.Instance.TryGet(target, out IDevCommand found) == false)
			{
				context.LogError($"알 수 없는 명령: {target}");
				return;
			}

			context.LogInfo(found.Usage);
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return System.Linq.Enumerable.Empty<string>();

			string token = partial[0];
			return DevCommandRegistry.Instance.AllNames
				.Where(name => name.StartsWith(token, System.StringComparison.OrdinalIgnoreCase));
		}
	}
}
