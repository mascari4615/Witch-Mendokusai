using UnityEngine;

namespace WitchMendokusai
{
	public static class UIUtil
	{
		public static void SetVisible(this CanvasGroup canvasGroup, bool active, bool allowInteraction = true)
		{
			canvasGroup.alpha = active ? 1 : 0;
			canvasGroup.blocksRaycasts = allowInteraction && active;
			canvasGroup.interactable = allowInteraction && active;
		}
	}
}