using UnityEngine;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	public class FadeTackMixer : PlayableBehaviour
	{
		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
			// 트랙 바인딩 (FadeTrack 의 TrackBindingType). 디렉터가 프리팹에서 묶은 암전 CanvasGroup
			CanvasGroup canvasGroup = playerData as CanvasGroup;

			float currentAlpha = 0f;

			if (canvasGroup == false)
				return;

			int inputCount = playable.GetInputCount();
			for (int i = 0; i < inputCount; i++)
			{
				float inputWeight = playable.GetInputWeight(i);

				if (inputWeight > 0f)
				{
					currentAlpha = inputWeight;
				}
			}

			canvasGroup.alpha = currentAlpha;
		}
	}
}
