using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화 출력기 — 말풍선·시간 흐름 같은 Unity 쪽만 맡는 **얇은 껍데기**.
	///
	/// 진행 규칙(무엇이 언제 넘어가는가)은 전부 <see cref="DialoguePlayback"/> 에 있다. 여기 두면
	/// 검증이 PlayMode 로 올라가 버리기 때문이다 — 그쪽은 화면 없이 EditMode 에서 돈다.
	///
	/// 두 입구:
	/// <list type="bullet">
	/// <item><see cref="Play(DialogueGraph, Transform)"/> — 정본. 말하기·선택지·기다리기·분기 다 된다.</item>
	/// <item><see cref="Play(DialogueLine)"/> — TASK-WM-013 시절 선형 사슬. 옛 호출처 호환용으로 남긴다.</item>
	/// </list>
	///
	/// TASK-WM-078 γ P2-2 (2026-05-13) 부터 SceneLifetimeScope.RegisterComponentOnNewGameObject 가 spawn (UIManager AddComponent 폐기).
	/// </summary>
	public class DialogueRunner : MonoBehaviour
	{
		public static DialogueRunner Instance { get; private set; }

		public static bool TryGetExistingInstance(out DialogueRunner mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const float DEFAULT_LINE_DURATION = 3f;

		private Coroutine activeCoroutine;

		private UIManager uiManager;

		[Inject]
		public void Construct(UIManager uiManager)
		{
			this.uiManager = uiManager;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		private DialoguePlayback playback;
		private Transform bubbleTarget;

		/// <summary>선택지가 제시됐다 — UI 가 버튼을 그리고 <see cref="SubmitChoice"/> 로 돌려준다.</summary>
		public event Action<IReadOnlyList<string>> OnChoicesPresented = delegate { };

		/// <summary>대화가 끝났다(정상 종료·중단 공통).</summary>
		public event Action OnDialogueFinished = delegate { };

		public bool IsPlaying => playback != null && playback.IsPlaying;
		public DialogueLine CurrentLine => playback?.CurrentLine;
		public IReadOnlyList<string> CurrentChoices => playback?.CurrentChoices;

		/// <summary>대화 그래프 재생 — 이 게임에서 그래프를 실제로 쓰는 지점.</summary>
		public void Play(DialogueGraph graph, Transform speakerTransform = null)
		{
			if (graph == null)
			{
				Debug.LogWarning("[DialogueRunner] Play called with null DialogueGraph");
				return;
			}

			StopActive();

			bubbleTarget = ResolveTarget(speakerTransform);
			playback = new DialoguePlayback(graph) { DefaultSpeakSeconds = DEFAULT_LINE_DURATION };
			playback.OnStepChanged += HandleStepChanged;
			playback.OnFinished += HandlePlaybackFinished;

			activeCoroutine = StartCoroutine(DriveGraph());
			playback.Begin();
		}

		/// <summary>선택지 고르기 — UI 가 호출. Choice 스텝이 아니면 false.</summary>
		public bool SubmitChoice(int index) => playback != null && playback.SubmitChoice(index);

		/// <summary>바깥 사건 통지 — 사건 대기 노드를 푼다(퀘스트 완료 등).</summary>
		public void NotifyDialogueEvent(string eventId) => playback?.NotifyEvent(eventId);

		/// <summary>재생 중단. 재생 중이 아니면 아무 일도 안 한다.</summary>
		public void Stop() => StopActive();

		private IEnumerator DriveGraph()
		{
			while (playback != null && playback.IsPlaying)
			{
				playback.Tick(Time.deltaTime);
				yield return null;
			}
			activeCoroutine = null;
		}

		private void HandleStepChanged(DialogueStep step)
		{
			if (step.Kind == DialogueStepKind.Speak)
			{
				ShowBubble(step.SpeakLine);
				return;
			}
			if (step.Kind == DialogueStepKind.Choice)
			{
				OnChoicesPresented(step.Options);
			}
		}

		private void ShowBubble(DialogueLine line)
		{
			if (line == null)
			{
				return;
			}
			float duration = line.Wait > 0f ? line.Wait : DEFAULT_LINE_DURATION;
			if (uiManager != null && uiManager.SpeechBubble != null && bubbleTarget != null)
			{
				uiManager.SpeechBubble.Show(bubbleTarget, line.Text, duration);
			}
		}

		private void HandlePlaybackFinished()
		{
			OnDialogueFinished();
		}

		private void StopActive()
		{
			if (activeCoroutine != null)
			{
				StopCoroutine(activeCoroutine);
				activeCoroutine = null;
			}
			if (playback == null)
			{
				return;
			}

			playback.OnStepChanged -= HandleStepChanged;
			playback.OnFinished -= HandlePlaybackFinished;
			DialoguePlayback stopping = playback;
			playback = null;

			// 이미 끝난 재생을 또 「끝났다」고 알리지 않는다 — 구독 해제 후라 안쪽 통지는 안 온다.
			if (stopping.IsPlaying == false)
			{
				return;
			}
			stopping.Stop();
			OnDialogueFinished();
		}

		private static Transform ResolveTarget(Transform speakerTransform)
		{
			if (speakerTransform != null)
			{
				return speakerTransform;
			}
			Camera mainCamera = Camera.main;
			return mainCamera == null ? null : mainCamera.transform;
		}

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

			// 그래프 재생 중이었다면 그것부터 접는다 — 두 재생이 같은 말풍선을 동시에 쓰면 안 된다.
			StopActive();
			activeCoroutine = StartCoroutine(PlaySequence(first, speakerTransform));
		}

		private IEnumerator PlaySequence(DialogueLine first, Transform speakerTransform)
		{
			Transform target = ResolveTarget(speakerTransform);

			DialogueLine current = first;
			while (current != null)
			{
				Debug.Log($"[DialogueRunner] Speak: \"{current.Text}\" wait={current.Wait}");

				float duration = current.Wait > 0f ? current.Wait : DEFAULT_LINE_DURATION;

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
