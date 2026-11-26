using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// barrage , 직접 누르는 큰 포화 (R) 의 게이지를 바로 채움. 일제 사격을 든 판 안에서만.
	/// </summary>
	public class BarrageCommand : IDevCommand
	{
		public string Name => "barrage";
		public string Usage => "barrage";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			if (VolleyGauge.Current == null)
			{
				context.LogError("일제 사격을 들고 있지 않음");
				return;
			}
			VolleyGauge.Current.FillBarrage();
			context.LogSuccess($"큰 포화 {VolleyGauge.Current.BarrageFill01:P0}");
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
