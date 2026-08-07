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

		[Header("대사 노출 시간")]
		[Tooltip("초당 읽는 글자 수. 0 이면 안 쓰고 아래 기본 시간을 그대로 쓴다.")]
		[SerializeField] private float readingCharactersPerSecond = 11f;
		[Tooltip("아무리 짧아도 이만큼은 보여준다(초).")]
		[SerializeField] private float minimumLineSeconds = 1.2f;
		[Tooltip("아무리 길어도 이만큼에서 멈춘다(초). 0 이면 위 한계 없음.")]
		[SerializeField] private float maximumLineSeconds = 8f;

		[Tooltip("선택지가 떴는데 아무도 안 고르면 이만큼 뒤에 접는다(초). 0 이면 안 접는다.")]
		[SerializeField] private float choiceStallSeconds = 15f;

		private Coroutine activeCoroutine;

		private UIManager uiManager;
		private IDialogueEffectSink effectSink;
		private IDialogueItemCountSource itemCountSource;

		[Inject]
		public void Construct(UIManager uiManager, IEffectRunner effectRunner, SOManager soManager)
		{
			this.uiManager = uiManager;
			effectSink = new EffectRunnerDialogueSink(effectRunner);

			// 원고의 「그 물건 가졌나」가 실제 가방을 보게 한다. 등록 전이면 조건은 「없다」로 치므로
			// (터지지 않는다) 이 배선이 늦어도 대화 자체는 돈다 — 다만 그 선택지가 안 뜬다.
			itemCountSource = new InventoryDialogueItemSource(soManager.ItemInventory);
			DialogueItemBridge.Register(itemCountSource);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
			DialogueHistoryBridge.Register(History);
			coordinator.OnStartRequested += StartRequested;
		}

		private void OnDestroy()
		{
			coordinator.OnStartRequested -= StartRequested;
			DialogueItemBridge.Clear(itemCountSource);
			DialogueHistoryBridge.Clear(History);
			if (Instance == this)
				Instance = null;
		}

		private DialoguePlayback playback;
		private Transform bubbleTarget;
		private DialogueGraph playingGraph;

		// 원고로 재생 중이면 그 자산 ID — 이력은 그래프가 아니라 *원고* 단위로 남아야 한다
		// (그래프는 원고에서 매번 세워지는 사본이라 ID 가 없다).
		private int playingScriptId = DataSO.NONE_ID;

		/// <summary>「이 대화를 본 적 있나」 기록 — 조건이 <see cref="DialogueHistoryBridge"/> 로 찾아온다.</summary>
		public DialogueHistory History { get; } = new();

		/// <summary>지나간 대사 — 「방금 뭐라고 했지」를 위해 남긴다(저장 대상 아님).</summary>
		public DialogueTranscript Transcript { get; } = new();

		// 「언제 거는가」는 조정자가 정한다(순수 — 화면 없이 검증된다). 러너는 「어떻게 거는가」만 맡는다.
		private readonly DialoguePlayCoordinator coordinator = new();

		/// <summary>선택지가 제시됐다 — UI 가 버튼을 그리고 <see cref="SubmitChoice"/> 로 돌려준다.</summary>
		public event Action<IReadOnlyList<string>> OnChoicesPresented = delegate { };

		/// <summary>대화가 끝났다(정상 종료·중단 공통).</summary>
		public event Action OnDialogueFinished = delegate { };

		public bool IsPlaying => playback != null && playback.IsPlaying;
		public DialogueLine CurrentLine => playback?.CurrentLine;
		public IReadOnlyList<string> CurrentChoices => playback?.CurrentChoices;

		/// <summary>기다리는 대화 수(지금 재생 중인 것은 안 센다).</summary>
		public int PendingCount => coordinator.PendingCount;

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
			playingGraph = graph;
			playingScriptId = DataSO.NONE_ID;
			History.MarkStarted(graph.ID);
			playback = new DialoguePlayback(graph, effectSink)
			{
				DefaultSpeakSeconds = DEFAULT_LINE_DURATION,
				ReadingCharactersPerSecond = readingCharactersPerSecond,
				MinimumSpeakSeconds = minimumLineSeconds,
				MaximumSpeakSeconds = maximumLineSeconds,
				ChoiceStallSeconds = choiceStallSeconds,
			};
			playback.OnChoiceStalled += HandleChoiceStalled;
			playback.OnStepChanged += HandleStepChanged;
			playback.OnFinished += HandlePlaybackFinished;

			activeCoroutine = StartCoroutine(DriveGraph());
			playback.Begin();
		}

		/// <summary>
		/// 글로 쓴 대화 재생 — 원고 → 화면의 마지막 칸. 대화 이력에는 이 자산의 ID 가 남는다
		/// (그래야 「이 대화 봤나」가 원고 단위로 물어진다).
		/// </summary>
		public void Play(DialogueScriptSource source, Transform speakerTransform = null)
		{
			if (source == null)
			{
				Debug.LogWarning("[DialogueRunner] Play called with null DialogueScriptSource");
				return;
			}

			// 「지금 걸지 줄 세울지」는 조정자가 정한다(순수 — 화면 없이 검증된다).
			if (coordinator.Request(new DialoguePlayRequest(source, null, speakerTransform)) == false)
			{
				Debug.LogWarning($"[DialogueRunner] 대화 차례가 꽉 찼거나 이미 줄에 있다 — 흘림: {source.name}");
			}
		}

		/// <summary>조정자가 「이걸 걸어라」 할 때 실제로 거는 자리.</summary>
		private void StartRequested(DialoguePlayRequest request)
		{
			if (request.Script != null)
			{
				StartScript(request.Script, request.SpeakerTransform);
				return;
			}
			StartLine(request.Line, request.SpeakerTransform);
		}

		private void StartScript(DialogueScriptSource source, Transform speakerTransform)
		{
			DialogueGraph graph = source.BuildGraph(out ParsedDialogueScript parsed);
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				// 원고의 오타는 재생을 막지 않는다 — 다만 조용히 넘어가지도 않는다(줄 번호까지 찍는다).
				Debug.LogWarning($"[DialogueRunner] {source.name} L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}");
			}

			Play(graph, speakerTransform);
			History.MarkStarted(source.ID);
			playingScriptId = source.ID;
		}

		/// <summary>선택지 고르기 — UI 가 호출. Choice 스텝이 아니면 false.</summary>
		public bool SubmitChoice(int index) => playback != null && playback.SubmitChoice(index);

		/// <summary>바깥 사건 통지 — 사건 대기 노드를 푼다(퀘스트 완료 등).</summary>
		public void NotifyDialogueEvent(string eventId) => playback?.NotifyEvent(eventId);

		/// <summary>재생 중단 — 기다리던 것도 같이 접는다(「지금 대화 그만」이면 그게 맞다).</summary>
		public void Stop()
		{
			coordinator.Reset();
			StopActive();
		}

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
				Transcript.Record(step.SpeakLine);
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
			// 말풍선이 사라지는 시간과 대화가 넘어가는 시간은 **같아야 한다** — 다르면 빈 자리가 생기거나
			// 다음 대사가 앞 말풍선 위에 겹친다. 그래서 같은 계산을 쓴다.
			float duration = line.Wait > 0f
				? line.Wait
				: DialogueReadingTime.For(line.Text, readingCharactersPerSecond, minimumLineSeconds, maximumLineSeconds);
			if (duration <= 0f)
			{
				duration = DEFAULT_LINE_DURATION;
			}
			Transform anchor = ResolveLineAnchor(line);
			if (uiManager != null && uiManager.SpeechBubble != null && anchor != null)
			{
				uiManager.SpeechBubble.Show(anchor, line.Text, duration);
			}
		}

		/// <summary>
		/// 이 대사를 **누구 위에** 띄울지. 순서: ① 원고에 쓴 이름으로 등록된 캐릭터
		/// ② 재생할 때 넘겨받은 대상 ③ 카메라(옛 거동).
		///
		/// ①이 없다고 대화가 멈추면 안 된다 — 캐릭터 배선이 아직인 원고도 그냥 읽혀야 한다.
		/// </summary>
		private Transform ResolveLineAnchor(DialogueLine line)
		{
			string speakerName = line.ResolveSpeakerName();
			if (string.IsNullOrEmpty(speakerName) == false
				&& DialogueSpeakerBridge.TryGetAnchor(speakerName, out Transform speakerAnchor))
			{
				return speakerAnchor;
			}
			return bubbleTarget;
		}

		/// <summary>
		/// 선택지가 떴는데 고르는 쪽이 없다 — **선택지 화면이 아직 없어서** 생기는 상황이다.
		/// 조용히 서 있으면 뒤에 줄 선 대화까지 전부 막히므로, 크게 알리고 이 대화만 접는다(줄은 계속 흐른다).
		/// </summary>
		private void HandleChoiceStalled()
		{
			Debug.LogWarning($"[DialogueRunner] 선택지가 {choiceStallSeconds}초째 그대로다 — 고르는 쪽(선택지 화면)이 없다. 이 대화를 접는다.");
			playback?.Stop();
		}

		private void HandlePlaybackFinished()
		{
			// 끝까지 간 것만 「들었다」로 남긴다 — 중간에 접은 대화는 다음에 다시 보여줘야 한다.
			if (playingGraph != null && playback != null && playback.ReachedEnd)
			{
				History.MarkCompleted(playingScriptId == DataSO.NONE_ID ? playingGraph.ID : playingScriptId);
			}
			OnDialogueFinished();

			// 지금 막 끝난 재생을 정리한 *뒤* 다음 것을 건다 — 안 그러면 「재생 중」으로 보여 또 줄을 선다.
			playback = null;
			playingGraph = null;
			coordinator.NotifyFinished();
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
			playback.OnChoiceStalled -= HandleChoiceStalled;
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

			// 옛 입구도 같은 줄에 선다 — 두 입구가 서로를 끊으면 한쪽이 통째로 사라진다.
			coordinator.Request(new DialoguePlayRequest(null, first, speakerTransform));
		}

		private void StartLine(DialogueLine first, Transform speakerTransform)
		{
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
				Transcript.Record(current);

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
			coordinator.NotifyFinished();
		}
	}
}
