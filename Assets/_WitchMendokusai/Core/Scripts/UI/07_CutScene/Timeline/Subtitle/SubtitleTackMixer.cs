using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace WitchMendokusai
{
	public class SubtitleTackMixer : PlayableBehaviour
	{
		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
			// TextMeshProUGUI text = playerData as TextMeshProUGUI;
#if UNITY_EDITOR
			TextMeshProUGUI text = Object.FindFirstObjectByType<UIManager>().CutSceneModule.Subtitle;
#else
        TextMeshProUGUI text = UIManager.Instance.CutSceneModule.Subtitle;
#endif

			string currentText = string.Empty;
			float currentAlpha = 0f;

			if (!text)
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