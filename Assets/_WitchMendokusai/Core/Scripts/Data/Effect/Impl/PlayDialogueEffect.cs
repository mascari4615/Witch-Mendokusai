using UnityEngine;

namespace WitchMendokusai
{
	public class PlayDialogueEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			DialogueLine dialogueLine = effectInfo.Data as DialogueLine;
			if (dialogueLine == null)
			{
				Debug.LogWarning($"[PlayDialogueEffect] effectInfo.Data is not a DialogueLine: {effectInfo.Data}");
				return;
			}

			Debug.Log($"[PlayDialogueEffect] (stub) Play DialogueLine: {dialogueLine.name} text=\"{dialogueLine.Text}\"");
		}
	}
}
