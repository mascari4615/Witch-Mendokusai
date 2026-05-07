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

			DialogueRunner runner = DialogueRunner.Instance;
			if (runner == null)
			{
				Debug.LogWarning($"[PlayDialogueEffect] DialogueRunner.Instance is null — UIManager.Awake 가 실행되었는지 확인");
				return;
			}

			runner.Play(dialogueLine);
		}
	}
}
