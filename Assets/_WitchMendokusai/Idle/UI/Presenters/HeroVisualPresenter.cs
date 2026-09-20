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

		/// <summary>작은 칸 (카드, 편성 자리) 은 얼굴만. 반신을 줄이면 얼굴이 안 보인다</summary>
		public void SetFace(VisualElement element, int heroId)
		{
			Sprite face = catalog.FaceOf(heroId);
			element.style.backgroundImage = face != null
				? new StyleBackground(face)
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
