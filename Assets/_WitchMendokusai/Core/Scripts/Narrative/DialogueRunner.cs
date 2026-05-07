using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// DialogueLine 시퀀스 출력기. TASK-WM-013 prototype — SpeechBubble 위로 텍스트 출력 + Wait 대기 + Choices[0] 자동 다음 라인.
	/// 정사 (TASK-WM-052 Phase 2 — 노드 그래프 통합) 에서 Choice 분기 / Branch / Wait 노드 등으로 확장 예정.
	/// UIManager 가 Awake 시 AddComponent — UIRoot GameObject 에 attach.
	/// </summary>
	public class DialogueRunner : Singleton<DialogueRunner>
	{
		private const float DEFAULT_LINE_DURATION = 3f;

		private Coroutine activeCoroutine;

		public void Play(DialogueLine first)
		{
			Play(first, null);
		}

		public void Play(DialogueLine first, Transform speakerTransform)
		{
			if (first == null)
			{
				Debug.LogWarning("[DialogueRunner] Play called with null DialogueLine");
				return;
			}

			if (activeCoroutine != null)
				StopCoroutine(activeCoroutine);
			activeCoroutine = StartCoroutine(PlaySequence(first, speakerTransform));
		}

		private IEnumerator PlaySequence(DialogueLine first, Transform speakerTransform)
		{
			Transform target = speakerTransform;
			if (target == null)
			{
				Camera mainCamera = Camera.main;
				if (mainCamera != null)
					target = mainCamera.transform;
			}

			DialogueLine current = first;
			while (current != null)
			{
				Debug.Log($"[DialogueRunner] Speak: \"{current.Text}\" wait={current.Wait}");

				float duration = current.Wait > 0f ? current.Wait : DEFAULT_LINE_DURATION;

				UIManager uiManager = UIManager.Instance;
				if (uiManager != null && uiManager.SpeechBubble != null && target != null)
					uiManager.SpeechBubble.Show(target, current.Text, duration);

				yield return new WaitForSeconds(duration);

				if (current.Choices.Count > 0)
					current = current.Choices[0];
				else
					current = null;
			}

			activeCoroutine = null;
		}
	}
}
