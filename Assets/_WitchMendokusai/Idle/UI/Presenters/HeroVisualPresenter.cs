using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	public sealed class HeroVisualPresenter
	{
		private readonly HeroCatalogSO catalog;

		public HeroVisualPresenter(HeroCatalogSO catalog)
		{
			this.catalog = catalog;
		}

		public void SetPortrait(VisualElement element, int heroId)
		{
			Sprite portrait = catalog.SpriteOf(heroId);
			element.style.backgroundImage = portrait != null
				? new StyleBackground(portrait)
				: StyleKeyword.None;
		}

		public void SetAxis(VisualElement element, IdleHeroAxis axis)
		{
			for (int index = 0; index < 4; index++)
			{
				element.EnableInClassList("idle-hero-icon--" + index, index == (int)axis);
			}
		}

		public void SetStars(VisualElement element, int stars)
		{
			for (int index = 1; index <= 4; index++)
			{
				element.EnableInClassList("idle-hero-grade-" + index, index == stars);
			}
		}
	}
}
