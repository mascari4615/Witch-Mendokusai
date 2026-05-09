using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// give &lt;itemRef&gt; [amount]
	/// itemRef: ID 정수 / `I_<num>` 코드 / Name 부분 (한글·영문). amount 기본 1.
	/// 매칭 0개 → error, 2+개 → 후보 목록 출력 + 실행 보류.
	/// </summary>
	public class GiveItemCommand : IDevCommand
	{
		public string Name => "give";
		public string Usage => "give <itemRef> [amount]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}

			string reference = args[0];
			int amount = 1;
			if (args.Length >= 2)
			{
				if (int.TryParse(args[1], out int parsed) == false || parsed <= 0)
				{
					context.LogError($"amount 가 양의 정수여야 함: '{args[1]}'");
					return;
				}
				amount = parsed;
			}

			ItemData target = DevItemResolver.Resolve(reference, out List<ItemData> matches);
			if (target == null)
			{
				if (matches.Count == 0)
				{
					context.LogError($"매칭되는 아이템 없음: '{reference}'");
					return;
				}

				context.LogWarn($"여러 후보 ({matches.Count}) — 더 정확히 입력:");
				for (int i = 0; i < matches.Count && i < 10; i++)
					context.LogWarn($"  I_{matches[i].ID}  {matches[i].Name}");
				if (matches.Count > 10)
					context.LogWarn($"  ... 외 {matches.Count - 10}개");
				return;
			}

			SOManager.Instance.ItemInventory.Add(target, amount);
			context.LogSuccess($"{target.Name} x{amount} 지급 (ID {target.ID})");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			// give 의 첫 인자 (itemRef) 만 자동완성 — 두 번째 (amount) 는 자유 정수
			if (partial.Length != 1)
				return System.Linq.Enumerable.Empty<string>();

			return DevItemResolver.SuggestReferences(partial[0]);
		}
	}
}
