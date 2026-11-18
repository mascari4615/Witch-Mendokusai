using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// seed , 지금 판 시드 보기. seed fix , 지금 값으로 고정. seed &lt;value&gt; , 그 값으로 고정. seed off , 고정 해제.
	/// 고정은 다음 판부터 (재도전 포함).
	/// </summary>
	public class SeedCommand : IDevCommand
	{
		private const string FIX_ARG = "fix";
		private const string OFF_ARG = "off";

		public string Name => "seed";
		public string Usage => "seed [fix|off|<value>]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length > 0)
			{
				if (DevCommandArgs.Is(args[0], FIX_ARG))
					RunSeed.SetDevOverride(RunSeed.Current);
				else if (DevCommandArgs.Is(args[0], OFF_ARG))
					RunSeed.SetDevOverride(null);
				else if (int.TryParse(args[0], out int value))
					RunSeed.SetDevOverride(value);
				else
				{
					context.LogError(Usage);
					return;
				}
			}
			context.LogInfo(Describe());
		}

		public static string Describe() => $"지금 판 시드 {RunSeed.Current}, 고정 {(RunSeed.DevOverride.HasValue ? RunSeed.DevOverride.Value.ToString() : "없음")} (다음 판부터)";

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();
			return DevCommandArgs.StartsWith(new[] { FIX_ARG, OFF_ARG }, partial[0]);
		}
	}
}
