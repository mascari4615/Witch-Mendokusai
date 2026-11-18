using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// timescale [scale] , 시간 배율. 1 이 보통, 0.25 느리게, 4 빠르게. 정지와 hit stop 위에 곱함. 인자 없으면 지금 값.
	/// </summary>
	public class TimeScaleCommand : IDevCommand
	{
		public string Name => "timescale";
		public string Usage => "timescale [scale]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogInfo($"지금 배율 {TimeManager.Instance.DebugScale}");
				return;
			}
			if (DevCommandArgs.TryFloat(args[0], out float scale) == false || scale < 0f)
			{
				context.LogError($"배율은 0 이상 숫자: '{args[0]}'");
				return;
			}
			TimeManager.Instance.SetDebugScale(scale);
			context.LogSuccess($"시간 배율 {TimeManager.Instance.DebugScale}");
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
