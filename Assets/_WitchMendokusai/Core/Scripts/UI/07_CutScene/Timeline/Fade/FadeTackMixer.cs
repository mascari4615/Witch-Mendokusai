using UnityEngine;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	public class FadeTackMixer : PlayableBehaviour
	{
		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
#if UNITY_EDITOR
			CanvasGroup canvasGroup = Object.FindFirstObjectByType<UIManager>().CutSceneModule.FadeCanvasGroup;
#else
        CanvasGroup canvasGroup = UIManager.Instance.CutSceneModule.FadeCanvasGroup;
#endif

			float currentAlpha = 0f;

			if (!canvasGroup)
				return;

			int inputCount = playable.GetInputCount();
			for (int i = 0; i < inputCount; i++)
			{
				float inputWeight = playable.GetInputWeight(i);

				if (inputWeight > 0f)
				{
					ScriptPlayable<FadeBehaviour> inputPlayable =
						(ScriptPlayable<FadeBehaviour>)playable.GetInput(i);

					FadeBehaviour input = inputPlayable.GetBehaviour();
					// currentAlpha = input.alpha;
					currentAlpha = inputWeight;
				}
			}

			canvasGroup.alpha = currentAlpha;
		}
	}
}