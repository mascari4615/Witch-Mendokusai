using UnityEngine;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	public class FadeBehaviour : PlayableBehaviour
	{
		// public float alpha;

		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
#if UNITY_EDITOR
			CanvasGroup canvasGroup = Object.FindFirstObjectByType<UIManager>().CutSceneModule.FadeCanvasGroup;
#else
        CanvasGroup canvasGroup = UIManager.Instance.CutSceneModule.FadeCanvasGroup;
#endif
			// canvasGroup.alpha = alpha;
		}
	}
}