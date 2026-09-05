using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (ctx 불요 Effect — context 무시).
	public class PlayDialogueEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			DialogueRunner runner = DialogueRunner.Instance;
			if (runner == null)
			{
				Debug.LogWarning($"[PlayDialogueEffect] DialogueRunner.Instance is null — UIManager.Awake 가 실행되었는지 확인");
				return;
			}

			// 글로 쓴 대화(TASK-WM-052) — 퀘스트·이벤트가 **원고 한 편을 통째로** 부르는 길.
			// 이게 없으면 원고를 써 놔도 게임 안에서 시작시킬 방법이 없다(선택지·조건·보상까지 다 딸려 온다).
			if (effectInfo.Data is DialogueScriptSource dialogueScript)
			{
				runner.Play(dialogueScript);
				return;
			}

			// 옛 방식 — 대사 한 줄짜리 자산. 부르는 데이터가 아직 있으니 그대로 둔다.
			if (effectInfo.Data is DialogueLine dialogueLine)
			{
				runner.Play(dialogueLine);
				return;
			}

			Debug.LogWarning($"[PlayDialogueEffect] 대화가 아닌 것을 재생하라고 했다: {effectInfo.Data}");
		}
	}
}
