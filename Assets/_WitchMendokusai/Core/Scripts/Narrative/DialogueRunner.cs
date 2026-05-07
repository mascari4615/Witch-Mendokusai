using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대사 시퀀스를 순차 재생하는 Singleton.
	/// UI 는 이벤트 (OnLineStart / OnLineEnd / OnChoicesPresented / OnDialogueComplete) 를 구독.
	/// 캐릭터 표시·typewriter·portrait 는 UI side 책임 — Runner 는 *진행 흐름* 만.
	///
	/// Wait 의미:
	///   line.Wait &gt; 0 — Wait 초 후 자동 진행. Advance() 호출로 조기 진행 가능.
	///   line.Wait &lt;= 0 — 무한 대기. Advance() 호출 시에만 진행 (기본값, 플레이어 페이스).
	///
	/// Choices 의미: line.Choices.Count &gt; 0 이면 OnChoicesPresented 발화 + SubmitChoice 호출 대기.
	/// 선택 후 chosen line 을 *재귀* 로 진행 (chosen 자체가 다음 라인). Phase 2 ChoiceNode 에서 정식 분기로 대체 예정.
	/// </summary>
	public class DialogueRunner : Singleton<DialogueRunner>
	{
		public Action<DialogueLine> OnLineStart = delegate { };
		public Action<DialogueLine> OnLineEnd = delegate { };
		public Action OnDialogueComplete = delegate { };
		public Action<IReadOnlyList<DialogueLine>> OnChoicesPresented = delegate { };

		public bool IsPlaying { get; private set; }
		public DialogueLine CurrentLine { get; private set; }
		public IReadOnlyList<DialogueLine> CurrentChoices { get; private set; }

		private Coroutine playCoroutine;
		private bool advanceRequested;
		private DialogueLine choiceSelected;

		public void Play(DialogueLine singleLine)
		{
			if (singleLine == null)
			{
				Debug.LogWarning("DialogueRunner.Play: null line");
				return;
			}

			Play(new List<DialogueLine> { singleLine });
		}

		public void Play(IReadOnlyList<DialogueLine> sequence)
		{
			if (sequence == null || sequence.Count == 0)
			{
				Debug.LogWarning("DialogueRunner.Play: empty sequence");
				return;
			}

			if (IsPlaying)
				Stop();

			playCoroutine = StartCoroutine(PlayCoroutine(sequence));
		}

		public void Advance()
		{
			if (IsPlaying == false)
				return;

			advanceRequested = true;
		}

		public void Stop()
		{
			if (playCoroutine != null)
			{
				StopCoroutine(playCoroutine);
				playCoroutine = null;
			}

			IsPlaying = false;
			CurrentLine = null;
			CurrentChoices = null;
			choiceSelected = null;
		}

		public void SubmitChoice(DialogueLine choice)
		{
			if (CurrentChoices == null)
			{
				Debug.LogWarning("DialogueRunner.SubmitChoice: no choices presented");
				return;
			}

			if (CurrentChoices.Contains(choice) == false)
			{
				Debug.LogWarning($"DialogueRunner.SubmitChoice: choice {choice} not in CurrentChoices");
				return;
			}

			choiceSelected = choice;
		}

		private IEnumerator PlayCoroutine(IReadOnlyList<DialogueLine> sequence)
		{
			IsPlaying = true;

			for (int index = 0; index < sequence.Count; index++)
				yield return PlayLine(sequence[index]);

			CurrentLine = null;
			IsPlaying = false;
			playCoroutine = null;
			OnDialogueComplete();
		}

		private IEnumerator PlayLine(DialogueLine line)
		{
			CurrentLine = line;
			OnLineStart(line);

			advanceRequested = false;
			float elapsed = 0f;
			bool waitForAdvance = line.Wait <= 0f;

			while (true)
			{
				if (advanceRequested)
					break;

				if (waitForAdvance == false && elapsed >= line.Wait)
					break;

				elapsed += Time.deltaTime;
				yield return null;
			}

			OnLineEnd(line);

			if (line.Choices != null && line.Choices.Count > 0)
			{
				CurrentChoices = line.Choices;
				choiceSelected = null;
				OnChoicesPresented(CurrentChoices);

				while (choiceSelected == null)
					yield return null;

				DialogueLine chosen = choiceSelected;
				CurrentChoices = null;
				choiceSelected = null;

				yield return PlayLine(chosen);
			}
		}
	}
}
