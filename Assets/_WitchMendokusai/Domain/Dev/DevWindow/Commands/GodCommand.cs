using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// god [on|off] , 주인공 무적. 인자 없으면 뒤집기. 낙사처럼 무적 관통 피해는 받음.
	/// </summary>
	public class GodCommand : IDevCommand
	{
		private const string ON_ARG = "on";
		private const string OFF_ARG = "off";

		public string Name => "god";
		public string Usage => "god [on|off]";

		public void Execute(DevCommandContext context, string[] args)
		{
			PlayerObject player = PlayerProvider.Instance.CurrentObject;
			bool next = player.IsDevInvincible == false;
			if (args.Length > 0)
			{
				if (DevCommandArgs.Is(args[0], ON_ARG))
					next = true;
				else if (DevCommandArgs.Is(args[0], OFF_ARG))
					next = false;
				else
				{
					context.LogError(Usage);
					return;
				}
			}
			player.IsDevInvincible = next;
			context.LogSuccess($"무적 {(next ? "켬" : "끔")}");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();
			return DevCommandArgs.StartsWith(new[] { ON_ARG, OFF_ARG }, partial[0]);
		}
	}
}
