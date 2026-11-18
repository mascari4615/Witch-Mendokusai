using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// weapon &lt;itemRef|none&gt; , 든 무기를 전부 걷고 이 무기 하나만. none 이면 맨손.
	/// 장비 능력치 효과는 넣지 않음 (반복 장착에도 능력치가 쌓이지 않게). 판 안에서만.
	/// </summary>
	public class WeaponCommand : IDevCommand
	{
		private const string NONE_ARG = "none";
		private const string CODE_PREFIX = "I_";

		public string Name => "weapon";
		public string Usage => "weapon <itemRef|none>";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;

			if (DevCommandArgs.Is(args[0], NONE_ARG))
			{
				DevCombatTools.EquipOnly(null);
				context.LogSuccess("맨손");
				return;
			}

			List<EquipmentData> matches = DevCombatTools.AllWeapons().Where(weapon => Matches(weapon, args[0])).ToList();
			if (matches.Count != 1)
			{
				context.LogError(matches.Count == 0 ? $"맞는 무기 없음: '{args[0]}'" : $"후보 {matches.Count}개, 더 정확히: {string.Join(", ", matches.Select(weapon => CODE_PREFIX + weapon.ID))}");
				return;
			}

			DevCombatTools.EquipOnly(matches[0]);
			context.LogSuccess($"{matches[0].Name} 하나만 장착");
		}

		private static bool Matches(EquipmentData weapon, string reference)
		{
			if (int.TryParse(reference, out int id))
				return weapon.ID == id;
			if (reference.StartsWith(CODE_PREFIX, System.StringComparison.OrdinalIgnoreCase) && int.TryParse(reference.Substring(CODE_PREFIX.Length), out int codeId))
				return weapon.ID == codeId;
			return weapon.Name != null && weapon.Name.IndexOf(reference, System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();
			return DevCommandArgs.StartsWith(DevCombatTools.AllWeapons().Select(weapon => CODE_PREFIX + weapon.ID).Append(NONE_ARG), partial[0]);
		}
	}
}
