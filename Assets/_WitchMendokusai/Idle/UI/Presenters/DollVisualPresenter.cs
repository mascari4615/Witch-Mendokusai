using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	public sealed class DollVisualPresenter
	{
		private readonly DollCatalogSO catalog;

		public DollVisualPresenter(DollCatalogSO catalog)
		{
			this.catalog = catalog;
		}

		public void SetPortrait(VisualElement element, int dollId)
		{
			Sprite portrait = catalog.SpriteOf(dollId);
			element.style.backgroundImage = portrait != null
				? new StyleBackground(portrait)
				: StyleKeyword.None;
		}

		/// <summary>작은 칸 (카드, 편성 자리) 은 얼굴만. 반신을 줄이면 얼굴이 안 보인다</summary>
		public void SetFace(VisualElement element, int dollId)
		{
			Sprite face = catalog.FaceOf(dollId);
			element.style.backgroundImage = face != null
				? new StyleBackground(face)
				: StyleKeyword.None;
		}

		public void SetAxis(VisualElement element, IdleDollAxis axis)
		{
			for (int index = 0; index < 4; index++)
			{
				element.EnableInClassList("idle-doll-icon--" + index, index == (int)axis);
			}
		}

		public void SetStars(VisualElement element, int stars)
		{
			for (int index = 1; index <= 4; index++)
			{
				element.EnableInClassList("idle-doll-grade-" + index, index == stars);
			}
		}
	}
}
