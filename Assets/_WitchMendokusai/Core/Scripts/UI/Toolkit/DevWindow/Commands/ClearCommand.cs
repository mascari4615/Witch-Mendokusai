using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// clear — 콘솔 출력 비우기.
	/// </summary>
	public class ClearCommand : IDevCommand
	{
		public string Name => "clear";
		public string Usage => "clear";

		public void Execute(DevCommandContext context, string[] args)
		{
			context.Console.Clear();
		}

		public IEnumerable<string> Suggest(string[] partial) => System.Linq.Enumerable.Empty<string>();
	}
}
