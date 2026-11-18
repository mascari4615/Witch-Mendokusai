using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// levelup [count] , 경험치를 채워 레벨업 카드 선택을 엶. 판 안에서만.
	/// </summary>
	public class LevelUpCommand : IDevCommand
	{
		public string Name => "levelup";
		public string Usage => "levelup [count]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			int count = 1;
			if (args.Length > 0 && (int.TryParse(args[0], out count) == false || count < 1))
			{
				context.LogError($"횟수는 1 이상 정수: '{args[0]}'");
				return;
			}

			UnitStat stat = PlayerProvider.Instance.Current.UnitStat;
			// 경험치 칸 변경 알림으로 ExpManager 가 레벨을 올리고 카드 선택이 열림. 모으던 경험치는 남김
			for (int i = 0; i < count; i++)
				stat[UnitStatType.EXP_CUR] += stat[UnitStatType.EXP_MAX];
			context.LogSuccess($"레벨업 {count}번, 지금 레벨 {stat[UnitStatType.LEVEL_CUR]}");
		}

		public IEnumerable<string> Suggest(string[] partial) => Enumerable.Empty<string>();
	}
}
