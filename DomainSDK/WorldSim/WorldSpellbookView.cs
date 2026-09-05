using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계의 마도서를 <b>화면이 그릴 수 있는 모양</b>으로 바꾼다 (TASK-WM-217).
	///
	/// ★ 왜 필요한가 (실측 2026-08-10): 완성 보상은 이미 세계가 정하는데, <b>게임 화면의 목표와
	///   등급은 여전히 자기 자산(SO)으로 그렸다.</b> 세계의 마도서와 게임 자산이 한 글자라도 다르면
	///   사람은 「여기까지 저으면 된다」는 표시를 보고 저은 뒤 <b>딴 것을 받는다</b> —
	///   화면은 「최상급」인데 세계는 「조잡」인 상태도 만들어진다. 그건 같은 세계가 아니다.
	///   웹 창은 이미 세계의 마도서를 그린다. 게임 창만 자기 것을 그리고 있었다.
	///
	/// 여기는 <b>변환만</b> 한다 — 어느 쪽을 고를지·무엇이 나올지는 <see cref="WorldRecipeBook"/> 가 정한다.
	/// </summary>
	public static class WorldSpellbookView
	{
		/// <summary>마도서 한 쪽 → 화면이 그리는 목표(가운데·반경).</summary>
		public static BrewRecipe ToRecipe(RecipeCatalogEntry page)
		{
			if (page == null)
				return default;

			return new BrewRecipe
			{
				Id = page.id,
				EffectName = page.name,
				Target = new EffectTarget
				{
					Position = new BrewVector(page.targetX, page.targetY),
					Radius = page.radius <= 0f ? 0.2f : page.radius,
				},
			};
		}

		/// <summary>
		/// 지금 저은 자리에서 <b>사람이 노리고 있는 쪽</b> — 화면은 이 쪽을 목표로 그린다.
		///
		/// 고르는 규칙은 하나뿐이다: <b>가장 가까운 쪽</b>. 세계가 완성을 채점할 때도 「닿았나」를 보므로,
		/// 가까운 쪽을 그려 주면 화면의 거리·등급이 세계의 판정과 같은 것을 가리킨다.
		/// 아직 아무것도 안 저었으면(비었으면) 첫 쪽을 그린다 — 빈 화면보다 낫다.
		/// </summary>
		public static bool TryAim(IReadOnlyList<RecipeCatalogEntry> pages, BrewState state, out BrewRecipe aimed)
		{
			aimed = default;
			if (pages == null || pages.Count == 0)
				return false;

			RecipeCatalogEntry best = null;
			float bestDistance = float.MaxValue;

			for (int i = 0; i < pages.Count; i++)
			{
				RecipeCatalogEntry page = pages[i];
				if (page == null)
					continue;

				float dx = page.targetX - state.Position.X;
				float dy = page.targetY - state.Position.Y;
				float distance = (dx * dx) + (dy * dy);

				// 같은 거리면 먼저 적힌 쪽 — 목표가 프레임마다 흔들리면 사람은 조준을 못 한다.
				if (distance >= bestDistance)
					continue;

				best = page;
				bestDistance = distance;
			}

			if (best == null)
				return false;

			aimed = ToRecipe(best);
			return true;
		}
	}
}
