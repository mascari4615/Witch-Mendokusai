using System;
using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	/// <summary>마도서 한 쪽 — 「여기까지 저으면 이게 나온다」 (TASK-WM-217).</summary>
	[Serializable]
	public class RecipeCatalogEntry
	{
		public int id;
		public string name = string.Empty;
		public float targetX;
		public float targetY;
		public float radius = 0.2f;
		public int resultItemId;
		public int amount = 1;
	}

	/// <summary>세계가 아는 마도서 (아이템 목록과 같은 모양 — 정본은 게임 자산, 여기는 뽑아 담는 그릇).</summary>
	[Serializable]
	public class RecipeCatalogData
	{
		public RecipeCatalogEntry[] recipes = Array.Empty<RecipeCatalogEntry>();
	}

	/// <summary>완성 한 판의 결과 — 무엇이, 어느 등급으로, 몇 개 나왔나.</summary>
	public struct BrewCompletion
	{
		public BrewState State;
		public int RecipeId;
		public string RecipeName;
		public BrewGrade Grade;
		public float Quality;
		public int ResultItemId;
		public int Amount;

		/// <summary>
		/// 아무 쪽에도 못 닿았다 — 솥은 비지만 손에는 아무것도 없다.
		/// ⚠ 아이템 번호로 판단하지 않는다 (실측 2026-08-10): 게임의 <b>나무</b>가 0 번이라,
		///   결과가 나무인 레시피가 조용히 「빈 결과」로 읽힌다. 레시피 번호는 1부터라 안전하다.
		/// </summary>
		public bool Empty => RecipeId == 0;
	}

	/// <summary>
	/// 저은 자리가 <b>어느 쪽(레시피)에 닿았나</b>를 세계가 판정한다 (TASK-WM-217).
	///
	/// ★ 왜 세계인가: 전에는 서버가 「누가 먼저 눌렀나」만 정하고, <b>무엇이 나왔는지는 창이 정했다</b>.
	///   그러면 창을 고친 사람이 원하는 것을 뽑아낼 수 있다 — 그건 판정이 아니라 신고다.
	///   여기서 정하면 게임 창·웹 창이 같은 솥에서 같은 답을 받는다.
	///
	/// 여러 쪽에 동시에 닿으면 <b>품질이 가장 높은</b> 쪽을 준다(같으면 먼저 적힌 쪽 — 조용히 흔들리지 않게).
	/// </summary>
	public sealed class WorldRecipeBook
	{
		private readonly List<RecipeCatalogEntry> pages = new List<RecipeCatalogEntry>();
		private readonly BrewOutcomeRules rules;

		public WorldRecipeBook(RecipeCatalogData data) : this(data, BrewOutcomeRules.Default)
		{
		}

		public WorldRecipeBook(RecipeCatalogData data, BrewOutcomeRules rules)
		{
			this.rules = rules;
			if (data?.recipes == null)
				return;

			for (int i = 0; i < data.recipes.Length; i++)
			{
				RecipeCatalogEntry entry = data.recipes[i];
				// 번호 0 을 「없음」으로 거르지 않는다 — 0 은 실제 아이템(나무)이다.
				if (entry == null || entry.id == 0)
					continue;

				pages.Add(entry);
			}
		}

		/// <summary>아는 쪽 수 — 0이면 「빈 마도서」다(완성해도 아무것도 안 나온다).</summary>
		public int Count => pages.Count;

		/// <summary>쪽 그대로 — 창이 「무엇을 만들 수 있나」를 보이려면 필요하다.</summary>
		public IReadOnlyList<RecipeCatalogEntry> Pages => pages;

		/// <summary>이 솥이 닿은 쪽. 아무 데도 못 닿았으면 빈 결과.</summary>
		public BrewCompletion Judge(BrewState state)
		{
			BrewCompletion best = new BrewCompletion { State = state, Grade = BrewGrade.Failed };

			for (int i = 0; i < pages.Count; i++)
			{
				RecipeCatalogEntry page = pages[i];
				EffectTarget target = new EffectTarget
				{
					Position = new BrewVector(page.targetX, page.targetY),
					Radius = page.radius <= 0f ? 0.2f : page.radius,
				};

				BrewOutcome outcome = BrewEngine.Evaluate(state, target, rules);
				if (outcome.Reached == false)
					continue;

				// 이미 더 좋은 쪽을 찾았으면 그대로 둔다 — 같은 값이면 먼저 적힌 쪽이 이긴다.
				if (best.RecipeId != 0 && outcome.Quality <= best.Quality)
					continue;

				best = new BrewCompletion
				{
					State = state,
					RecipeId = page.id,
					RecipeName = page.name ?? string.Empty,
					Grade = outcome.Grade,
					Quality = outcome.Quality,
					ResultItemId = page.resultItemId,
					// 명품이면 하나 더 — 잘 저은 보람이 손에 남아야 사람이 다시 젓는다.
					Amount = (page.amount < 1 ? 1 : page.amount) + (outcome.Grade == BrewGrade.Masterwork ? 1 : 0),
				};
			}

			return best;
		}
	}
}
