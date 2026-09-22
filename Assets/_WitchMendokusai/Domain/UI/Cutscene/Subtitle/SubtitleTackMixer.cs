using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	public class SubtitleTackMixer : PlayableBehaviour
	{
		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
			// 트랙 바인딩 (SubtitleTrack 의 TrackBindingType). 디렉터가 프리팹에서 묶은 자막 글자
			TextMeshProUGUI text = playerData as TextMeshProUGUI;

			string currentText = string.Empty;
			float currentAlpha = 0f;

			if (text == false)
				return;

			int inputCount = playable.GetInputCount();
			for (int i = 0; i < inputCount; i++)
			{
				float inputWeight = playable.GetInputWeight(i);

				if (inputWeight > 0f)
				{
					ScriptPlayable<SubtitleBehaviour> inputPlayable =
						(ScriptPlayable<SubtitleBehaviour>)playable.GetInput(i);

					SubtitleBehaviour input = inputPlayable.GetBehaviour();
					currentText = input.subtitleText;
					currentAlpha = inputWeight;
				}
			}

			text.transform.parent.gameObject.SetActive(currentText != string.Empty);
			text.text = currentText;
			text.color = new Color(1, 1, 1, currentAlpha);
		}
	}
}
