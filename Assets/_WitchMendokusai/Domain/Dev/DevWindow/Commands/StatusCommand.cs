using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// status &lt;burn|poison|freeze|slow|stun|bleed|shock|mark|fear|silence|all&gt; [near|screen] [seconds]
	/// 가장 가까운 적 또는 화면 안 적 전부에 상태이상. 초를 비우면 상태이상 설정의 기본 지속.
	/// </summary>
	public class StatusCommand : IDevCommand
	{
		private const string ALL_ARG = "all";
		private const string NEAR_ARG = "near";
		private const string SCREEN_ARG = "screen";

		public string Name => "status";
		public string Usage => "status <burn|poison|freeze|slow|stun|bleed|shock|mark|fear|silence|all> [near|screen] [seconds]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}

			List<UnitStatusType> types = new();
			if (DevCommandArgs.Is(args[0], ALL_ARG))
				types.AddRange(UnitStatusRules.ALL);
			else if (DevCommandArgs.TryEnum(args[0], out UnitStatusType parsed))
				types.Add(parsed);
			else
			{
				context.LogError($"모르는 상태이상: '{args[0]}'");
				return;
			}

			DevCombatTools.StatusScope scope = DevCombatTools.StatusScope.Nearest;
			float seconds = 0f;
			for (int i = 1; i < args.Length; i++)
			{
				if (DevCommandArgs.Is(args[i], SCREEN_ARG))
					scope = DevCombatTools.StatusScope.Screen;
				else if (DevCommandArgs.Is(args[i], NEAR_ARG))
					scope = DevCombatTools.StatusScope.Nearest;
				else if (DevCommandArgs.TryFloat(args[i], out float parsedSeconds))
					seconds = parsedSeconds;
				else
					context.LogWarn($"모르는 인자 무시: '{args[i]}'");
			}

			int count = 0;
			foreach (UnitStatusType type in types)
				count = DevCombatTools.ApplyStatus(type, scope, seconds);

			if (count == 0)
				context.LogWarn("대상 몬스터 없음");
			else
				context.LogSuccess($"{string.Join(", ", types)} -> {count}마리");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length == 1)
				return DevCommandArgs.StartsWith(DevCommandArgs.EnumNames<UnitStatusType>().Append(ALL_ARG), partial[0]);
			if (partial.Length == 2)
				return DevCommandArgs.StartsWith(new[] { NEAR_ARG, SCREEN_ARG }, partial[1]);
			return Enumerable.Empty<string>();
		}
	}
}
