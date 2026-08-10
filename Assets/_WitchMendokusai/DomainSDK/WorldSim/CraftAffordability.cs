using System;
using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
	/// <summary>
	/// 만들 것 한 줄이 화면에 어떻게 보이나 (TASK-WM-217) — 재료 · 성공률 · <b>지금 되나</b>.
	///
	/// ★ 왜 판정 층인가: 웹 창은 이미 이 계산을 갖고 있는데(picker.mjs) 게임 창에는 없었다.
	///   없으면 사람은 「왜 안 되지」를 눌러 봐야 안다 — 그건 손해만 보는 배움이다.
	///   그리고 두 창이 <b>각자</b> 계산하면 언젠가 다른 말을 한다. 답은 골든 표
	///   (<c>wwwroot/picker-golden.json</c>)에 한 벌로 적혀 있고, 양쪽이 그것과 대조한다.
	/// </summary>
	public static class CraftAffordability
	{
		/// <summary>「나무 1/3, 석탄 0/1」 — 무엇이 얼마나 드는지, 지금 얼마나 있는지.</summary>
		public static string NeedsText(CraftRecipeEntry recipe, Func<int, int> carrying, Func<int, string> nameOf)
		{
			if (recipe?.items == null || recipe.items.Length == 0)
				return string.Empty;

			StringBuilder needs = new StringBuilder();
			for (int i = 0; i < recipe.items.Length; i++)
			{
				CraftIngredientEntry need = recipe.items[i];
				if (need == null)
					continue;

				if (needs.Length > 0)
					needs.Append(", ");

				// ⚠ 이름을 모르면 <b>번호로 버틴다</b> — 「재료」라고만 적으면 아무것도 안 알려 준다.
				//   (웹 창과 같은 규칙이다. 한때 여기만 「재료」라 두 창이 다른 글을 보여 줬다.)
				string material = nameOf == null ? null : nameOf(need.itemId);
				if (string.IsNullOrEmpty(material))
					material = "#" + need.itemId;

				needs.Append(material).Append(' ')
					.Append(carrying == null ? 0 : carrying(need.itemId))
					.Append('/').Append(need.amount);
			}

			return needs.ToString();
		}

		/// <summary>지금 그 줄대로 만들 수 있나 — 재료만 본다(주사위는 세계가 굴린다).</summary>
		public static bool CanCraft(CraftRecipeEntry recipe, Func<int, int> carrying)
		{
			if (recipe?.items == null)
				return true;

			for (int i = 0; i < recipe.items.Length; i++)
			{
				CraftIngredientEntry need = recipe.items[i];
				if (need == null || need.amount <= 0)
					continue;

				// ⚠ 번호 0(나무)도 진짜 재료다.
				int have = carrying == null ? 0 : carrying(need.itemId);
				if (have < need.amount)
					return false;
			}

			return true;
		}

		/// <summary>
		/// 고르개 한 칸의 글 — 「· 」가 앞에 붙으면 지금은 못 만든다는 뜻이다.
		/// 확실히 되는 줄(100%)에는 성공률을 안 붙인다 — 늘 붙이면 눈이 그 숫자를 흘려보낸다.
		/// </summary>
		public static string Label(CraftRecipeEntry recipe, Func<int, int> carrying, Func<int, string> nameOf)
		{
			string needs = NeedsText(recipe, carrying, nameOf);
			string luck = recipe.percentage >= 100f ? string.Empty : $" ({Trim(recipe.percentage)}%)";
			string head = CanCraft(recipe, carrying) ? string.Empty : "· ";

			return needs.Length == 0
				? $"{head}{recipe.name}{luck}"
				: $"{head}{recipe.name}{luck} — {needs}";
		}

		/// <summary>70 은 「70」으로, 62.5 는 「62.5」로 — 뒤에 붙는 0 은 안 보여 준다.</summary>
		private static string Trim(float percentage)
		{
			return percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
