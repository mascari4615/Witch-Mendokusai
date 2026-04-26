using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// quest unlock &lt;questRef&gt;
	/// questRef: ID 정수 / `Q_&lt;num&gt;` / Name 부분.
	/// 첫 인자는 subcommand (현재 unlock 만 지원). QuestManager.UnlockQuest 호출.
	/// </summary>
	public class UnlockQuestCommand : IDevCommand
	{
		public string Name => "quest";
		public string Usage => "quest unlock <questRef>";

		private const string CODE_PREFIX = "Q_";
		private const string SUBCMD_UNLOCK = "unlock";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 2 || string.Equals(args[0], SUBCMD_UNLOCK, StringComparison.OrdinalIgnoreCase) == false)
			{
				context.LogError(Usage);
				return;
			}

			string reference = args[1];
			QuestSO target = DevDataLookup.Resolve<QuestSO>(reference, CODE_PREFIX, out List<QuestSO> matches);
			if (target == null)
			{
				if (matches.Count == 0)
				{
					context.LogError($"매칭되는 퀘스트 없음: '{reference}'");
					return;
				}
				context.LogWarn($"여러 후보 ({matches.Count}) — 더 정확히 입력:");
				for (int i = 0; i < matches.Count && i < 10; i++)
					context.LogWarn($"  {CODE_PREFIX}{matches[i].ID}  {matches[i].Name}");
				if (matches.Count > 10)
					context.LogWarn($"  ... 외 {matches.Count - 10}개");
				return;
			}

			QuestManager.Instance.UnlockQuest(target);
			context.LogSuccess($"{target.Name} 언락 (ID {target.ID})");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			// partial[0] — subcommand 자동완성
			if (partial.Length == 1)
			{
				if (SUBCMD_UNLOCK.StartsWith(partial[0], StringComparison.OrdinalIgnoreCase))
					return new[] { SUBCMD_UNLOCK };
				return Enumerable.Empty<string>();
			}

			// partial[1] — quest ref
			if (partial.Length == 2 && string.Equals(partial[0], SUBCMD_UNLOCK, StringComparison.OrdinalIgnoreCase))
				return DevDataLookup.SuggestRefs<QuestSO>(partial[1], CODE_PREFIX);

			return Enumerable.Empty<string>();
		}
	}
}
