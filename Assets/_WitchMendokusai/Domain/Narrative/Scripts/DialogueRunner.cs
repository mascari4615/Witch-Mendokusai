using System;
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
	/// 입구는 셋인데 **길은 하나다** — 셋 다 차례를 서고, 세워진 그래프를 같은 방식으로 튼다.
	/// <list type="bullet">
	/// <item><see cref="Play(DialogueScriptSource, Transform)"/> — 정본. 원고 자산에서 세운다.</item>
	/// <item><see cref="Play(DialogueGraph, Transform)"/> — 이미 세워진 그래프(미리보기·시험·직접 호출).</item>
	/// <item><see cref="Play(DialogueLine)"/> — TASK-WM-013 시절 선형 사슬. 옛 호출처 호환용으로 남긴다.</item>
	/// </list>
	///
	/// ★ 입구마다 길이 따로면 한쪽은 반드시 뒤처진다. 실제로 옛 사슬은 건너뛰기·시간 주입을 못 받았고,
	///   그래프 입구는 차례를 안 서서 남의 대화를 끊었다. 지금은 셋 다 같은 자리로 모인다.
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

		[Tooltip("대사에 시간이 안 적혀 있고 읽는 속도도 안 쓸 때 머무는 시간(초). 0 이면 눌러야 넘어간다.")]
		[SerializeField] private float defaultLineSeconds = DEFAULT_LINE_DURATION;

		[Header("대사 노출 시간")]
		[Tooltip("초당 읽는 글자 수. 0 이면 안 쓰고 아래 기본 시간을 그대로 쓴다.")]
		[SerializeField] private float readingCharactersPerSecond = 11f;
		[Tooltip("아무리 짧아도 이만큼은 보여준다(초).")]
		[SerializeField] private float minimumLineSeconds = 1.2f;
		[Tooltip("아무리 길어도 이만큼에서 멈춘다(초). 0 이면 위 한계 없음.")]
		[SerializeField] private float maximumLineSeconds = 8f;

		[Tooltip("선택지가 떴는데 아무도 안 고르면 이만큼 뒤에 접는다(초). 0 이면 안 접는다.")]
		[SerializeField] private float choiceStallSeconds = 15f;

		[Header("담아 두는 양")]
		[Tooltip("지나간 대사를 몇 줄까지 들고 있나. 넘치면 오래된 것부터 버린다.")]
		[SerializeField] private int transcriptCapacity = DialogueTranscript.DEFAULT_CAPACITY;

		[Tooltip("동시에 몇 개까지 줄을 세우나. 꽉 차면 새로 온 것을 흘린다(경고를 찍는다).")]
		[SerializeField] private int dialogueQueueCapacity = DialoguePlayQueue.DEFAULT_CAPACITY;

		private UIManager uiManager;
		private IDialogueEffectSink effectSink;
		private IDialogueItemCountSource itemCountSource;
		private IDialogueQuestStateSource questStateSource;

		[Inject]
		public void Construct(UIManager uiManager, IEffectRunner effectRunner, SOManager soManager, QuestManager questManager)
		{
			this.uiManager = uiManager;
			effectSink = new EffectRunnerDialogueSink(effectRunner);

			// 원고의 「그 물건 가졌나」가 실제 가방을 보게 한다. 등록 전이면 조건은 「없다」로 치므로
			// (터지지 않는다) 이 배선이 늦어도 대화 자체는 돈다 — 다만 그 선택지가 안 뜬다.
			itemCountSource = new InventoryDialogueItemSource(soManager.ItemInventory);
			DialogueItemBridge.Register(itemCountSource);

			// 원고의 「그 의뢰 끝냈나」가 실제 진행을 보게 한다. 여기도 등록 전이면 「잠김」으로 치므로
			// 배선이 늦어도 대화는 돈다(그 대사가 안 나올 뿐).
			questStateSource = new QuestManagerDialogueQuestSource(questManager);
			DialogueQuestBridge.Register(questStateSource);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
			DialogueHistoryBridge.Register(History);
			EnsureCoordinatorWired();
		}

		/// <summary>
		/// 조정자에 「걸어라」를 받을 귀를 붙인다. 여러 번 불러도 한 번만 붙는다.
		///
		/// ★ 왜 Awake 만으로 부족한가: 다른 컴포넌트가 **자기 Awake 에서** 대화를 걸 수 있고,
		///   그때 이쪽 Awake 가 아직 안 돌았을 수 있다(유니티는 순서를 보장하지 않는다).
		///   그러면 그 대화도, 그 뒤에 줄 선 것도 전부 사라진다. 그래서 거는 자리에서도 확인한다.
		/// </summary>
		private void EnsureCoordinatorWired()
		{
			if (coordinatorWired)
			{
				return;
			}
			coordinatorWired = true;
			Coordinator.OnStartRequested += StartRequested;
		}

		/// <summary>
		/// 우리가 그 자리에서 세운 그래프를 버린다.
		///
		/// ★ 왜: 옛 대사 사슬은 틀 때마다 그래프 하나와 노드들을 새로 만든다. 안 버리면
		///   마을 사람에게 말 걸 때마다 하나씩 쌓인다(예전 코루틴 길은 아무것도 안 만들었다).
		///   원고에서 세운 그래프는 **자산이 들고 재사용**하므로 여기서 손대면 안 된다 — 그래서 표시를 본다.
		/// </summary>
		private void DiscardOwnedGraph()
		{
			if (ownsPlayingGraph == false)
			{
				return;
			}
			ownsPlayingGraph = false;
			if (playingGraph != null)
			{
				Destroy(playingGraph);
			}
		}

		private void OnDestroy()
		{
			Coordinator.OnStartRequested -= StartRequested;
			coordinatorWired = false;
			DialogueItemBridge.Clear(itemCountSource);
			DialogueQuestBridge.Clear(questStateSource);
			DialogueHistoryBridge.Clear(History);
			if (Instance == this)
				Instance = null;
		}

		private DialoguePlayback playback;
		private Transform bubbleTarget;
		private DialogueGraph playingGraph;

		// 지금 트는 대화의 번호 — 이력은 이 번호로 남는다. 원고로 틀면 원고 자산 번호,
		// 그래프를 직접 틀면 그 그래프 번호, 그 자리에서 세운 것이면 NONE_ID(= 안 남김).
		private int playingDialogueId = DataSO.NONE_ID;

		// 이 그래프를 우리가 만들었나 — 만들었으면 끝나고 버려야 한다(안 버리면 틀 때마다 쌓인다).
		private bool ownsPlayingGraph;

		/// <summary>「이 대화를 본 적 있나」 기록 — 조건이 <see cref="DialogueHistoryBridge"/> 로 찾아온다.</summary>
		public DialogueHistory History { get; } = new();

		private DialogueTranscript transcript;
		private DialoguePlayCoordinator coordinator;
		private bool coordinatorWired;

		/// <summary>
		/// 지나간 대사 — 「방금 뭐라고 했지」를 위해 남긴다(저장 대상 아님).
		///
		/// ★ 왜 첫 사용 때 만드나: 몇 줄까지 들고 있을지가 **인스펙터 값**이다.
		///   필드 초기화로 만들면 그 값이 읽히기 전에 만들어져서, 조절해도 안 먹는다.
		/// </summary>
		public DialogueTranscript Transcript =>
			transcript ??= new DialogueTranscript(
				transcriptCapacity <= 0 ? DialogueTranscript.DEFAULT_CAPACITY : transcriptCapacity);

		// 「언제 거는가」는 조정자가 정한다(순수 — 화면 없이 검증된다). 러너는 「어떻게 거는가」만 맡는다.
		// 줄 길이도 인스펙터 값이라 같은 이유로 첫 사용 때 만든다.
		private DialoguePlayCoordinator Coordinator =>
			coordinator ??= new DialoguePlayCoordinator(new DialoguePlayQueue(
				dialogueQueueCapacity <= 0 ? DialoguePlayQueue.DEFAULT_CAPACITY : dialogueQueueCapacity));

		/// <summary>선택지가 제시됐다 — UI 가 버튼을 그리고 <see cref="SubmitChoice"/> 로 돌려준다.</summary>
		public event Action<IReadOnlyList<string>> OnChoicesPresented = delegate { };

		/// <summary>대화가 끝났다(정상 종료·중단 공통).</summary>
		public event Action OnDialogueFinished = delegate { };

		public bool IsPlaying => playback != null && playback.IsPlaying;
		public DialogueLine CurrentLine => playback?.CurrentLine;
		public IReadOnlyList<string> CurrentChoices => playback?.CurrentChoices;

		/// <summary>기다리는 대화 수(지금 재생 중인 것은 안 센다).</summary>
		public int PendingCount => Coordinator.PendingCount;

		/// <summary>
		/// 대화 그래프 재생 — 이 게임에서 그래프를 실제로 쓰는 지점.
		///
		/// ★ 이 입구도 **줄을 선다.** 예전엔 여기만 조정자를 건너뛰고 바로 틀었다 —
		///   그래서 말하는 중에 그래프를 걸면 앞 대화가 그냥 끊기고, 반대도 마찬가지였다.
		///   어느 쪽도 「사라졌다」는 흔적을 안 남긴다. 입구가 셋이면 줄도 셋이 다 서야 한다.
		/// </summary>
		public void Play(DialogueGraph graph, Transform speakerTransform = null)
		{
			if (graph == null)
			{
				Debug.LogWarning("[DialogueRunner] Play called with null DialogueGraph");
				return;
			}

			EnsureCoordinatorWired();
			if (Coordinator.Request(new DialoguePlayRequest(null, null, speakerTransform, graph)) == false)
			{
				Debug.LogWarning($"[DialogueRunner] 대화 차례가 꽉 찼거나 이미 줄에 있다 — 흘림: {graph.name}");
			}
		}

		/// <summary>
		/// 조정자가 「이 그래프를 걸어라」 할 때 실제로 거는 자리.
		///
		/// ★ 번호를 **인자로 받는 이유**: 재생을 시작하면 그 자리에서 끝까지 갈 수도 있다
		///   (빈 그래프·못 읽은 원고). 그러면 「끝났다」 처리가 <see cref="playback"/> 을 만드는 줄 안에서
		///   먼저 일어나므로, 번호를 **그 뒤에** 적으면 이미 늦다 — 엉뚱한 번호로 기록되고,
		///   그 사이 걸린 다음 대화에 앞 대화의 번호가 찍힌다. 시작 전에 확정한다.
		/// </summary>
		/// <param name="dialogueId">
		/// 이력에 남길 번호. <see cref="DataSO.NONE_ID"/> 면 **아무것도 안 남긴다** —
		/// 옛 대사 사슬처럼 그 자리에서 세운 그래프는 번호가 없다(기본값 0 을 쓰면
		/// 0 번 자산을 「봤다」고 적어 버린다).
		/// </param>
		private void StartGraph(DialogueGraph graph, Transform speakerTransform, int dialogueId)
		{
			StopActive();
			DiscardOwnedGraph();

			bubbleTarget = ResolveTarget(speakerTransform);
			playingGraph = graph;
			playingDialogueId = dialogueId;
			if (dialogueId != DataSO.NONE_ID)
			{
				History.MarkStarted(dialogueId);
			}
			playback = new DialoguePlayback(graph, effectSink)
			{
				DefaultSpeakSeconds = defaultLineSeconds,
				ReadingCharactersPerSecond = readingCharactersPerSecond,
				MinimumSpeakSeconds = minimumLineSeconds,
				MaximumSpeakSeconds = maximumLineSeconds,
				ChoiceStallSeconds = choiceStallSeconds,
			};
			playback.OnChoiceStalled += HandleChoiceStalled;
			playback.OnChoiceSelected += HandleChoiceSelected;
			playback.OnStepChanged += HandleStepChanged;
			playback.OnFinished += HandlePlaybackFinished;

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
			EnsureCoordinatorWired();
			if (Coordinator.Request(new DialoguePlayRequest(source, null, speakerTransform)) == false)
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
			if (request.Graph != null)
			{
				StartGraph(request.Graph, request.SpeakerTransform, request.Graph.ID);
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

			// 이미 조정자가 「걸어라」 한 뒤다 — 여기서 또 줄을 서면 자기 뒤에 서서 영영 안 걸린다.
			// 번호는 **넘겨서** 시작 전에 박는다(시작하자마자 끝나는 원고가 있다).
			StartGraph(graph, speakerTransform, source.ID);
		}

		/// <summary>선택지 고르기 — UI 가 호출. Choice 스텝이 아니면 false.</summary>
		public bool SubmitChoice(int index) => playback != null && playback.SubmitChoice(index);

		/// <summary>
		/// 건너뛰기 — 선택지·사건 대기·끝 중 하나가 나올 때까지 넘긴다. 넘긴 스텝 수를 준다.
		///
		/// 아직 **누를 것이 없다**(입력 자산에 키를 안 붙였다). UI·입력이 붙기 전까지는
		/// 여기까지가 이 세션에서 할 수 있는 끝이다 — 부를 자리는 열어 둔다.
		/// </summary>
		public int Skip() => playback == null ? 0 : playback.Skip();

		/// <summary>바깥 사건 통지 — 사건 대기 노드를 푼다(퀘스트 완료 등).</summary>
		public void NotifyDialogueEvent(string eventId) => playback?.NotifyEvent(eventId);

		/// <summary>재생 중단 — 기다리던 것도 같이 접는다(「지금 대화 그만」이면 그게 맞다).</summary>
		public void Stop()
		{
			Coordinator.Reset();
			StopActive();
		}

		private void Update()
		{
			Tick(Time.deltaTime);
		}

		/// <summary>
		/// 시간 주입 — 그래프 재생을 그만큼 민다.
		///
		/// ★ 왜 코루틴을 걷어냈나: 예전엔 코루틴이 매 프레임 밀었다. 그러면 **화면 없이는 대화가 한 발도 못 간다** —
		///   시간을 주는 쪽이 유니티뿐이라, 「끝까지 갔을 때 다음 대화가 이어 걸리나」 같은 걸 볼 방법이 없었다.
		///   재생기가 이미 시간을 밖에서 받는 것과 같은 결로 맞춘다(그쪽이 화면 없이 검증되는 이유가 그것이다).
		///
		/// 게임에서는 <c>Update</c> 가 부른다. 재생 중이 아니면 아무 일도 안 한다.
		/// </summary>
		public void Tick(float deltaTime)
		{
			if (playback == null || playback.IsPlaying == false)
			{
				return;
			}
			playback.Tick(deltaTime);
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
				// 재생기에 넘긴 값과 **같은 값**이라야 한다 — 여기만 박아 두면 인스펙터로 조절했을 때
				// 말풍선과 대화가 서로 다른 시간을 쓰게 된다(위 주석이 경계하는 바로 그 어긋남).
				duration = defaultLineSeconds;
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

		/// <summary>
		/// 「이 대화에서 무슨 답을 골랐나」를 남긴다.
		///
		/// ★ 왜 재생기가 아니라 여기인가: 재생기는 **대화 번호를 모른다**(그래프만 안다).
		///   번호를 아는 건 무엇을 틀었는지 기억하는 이쪽이다. 끝까지 들었는지를 여기서 남기는 것과 같은 자리.
		///
		/// 도중에 접어도 남긴다 — 접었다고 **한 말이 없던 일이 되지는 않는다.**
		/// (「끝까지 들었나」와는 다른 물음이라 판단도 다르다.)
		/// </summary>
		private void HandleChoiceSelected(string label)
		{
			// 번호 없는 재생(그 자리에서 세운 사슬)은 남길 자리가 없다 — 0 번에 적으면 남의 칸을 더럽힌다.
			if (CurrentDialogueId != DataSO.NONE_ID)
			{
				History.MarkChoice(CurrentDialogueId, label);
			}

			// 로그에도 남긴다 — 되짚는 이유의 절반은 「내가 뭐라고 했더라」다.
			Transcript.RecordChoice(label);
		}

		/// <summary>지금 틀고 있는 대화의 번호. 번호 없는 재생이면 <see cref="DataSO.NONE_ID"/>.</summary>
		private int CurrentDialogueId => playingDialogueId;

		private void HandlePlaybackFinished()
		{
			// 끝까지 간 것만 「들었다」로 남긴다 — 중간에 접은 대화는 다음에 다시 보여줘야 한다.
			if (playingGraph != null && playback != null && playback.ReachedEnd
				&& CurrentDialogueId != DataSO.NONE_ID)
			{
				History.MarkCompleted(CurrentDialogueId);
			}
			OnDialogueFinished();

			// 지금 막 끝난 재생을 정리한 *뒤* 다음 것을 건다 — 안 그러면 「재생 중」으로 보여 또 줄을 선다.
			playback = null;
			DiscardOwnedGraph();
			playingGraph = null;
			Coordinator.NotifyFinished();
		}

		private void StopActive()
		{
			if (playback == null)
			{
				return;
			}

			playback.OnStepChanged -= HandleStepChanged;
			playback.OnFinished -= HandlePlaybackFinished;
			playback.OnChoiceStalled -= HandleChoiceStalled;
			playback.OnChoiceSelected -= HandleChoiceSelected;
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
			EnsureCoordinatorWired();
			Coordinator.Request(new DialoguePlayRequest(null, first, speakerTransform));
		}

		/// <summary>
		/// 옛 대사 사슬도 **그래프로 세워서 같은 길로** 튼다.
		///
		/// ★ 왜 바꿨나: 예전엔 이쪽만 코루틴이 직접 말풍선을 띄우며 걸었다. 그래서 이 길로 나온 대화는
		///   건너뛰기도, 시간 주입도, 로그의 고른 답도 못 받았다. 같은 일을 두 군데서 다르게 하면
		///   한쪽은 반드시 뒤처진다 — 실제로 뒤처져 있었다.
		///
		/// 뜻은 그대로다: 갈래가 여럿이어도 첫째만 간다(옛 동작). 다만 이제 **버린 가지를 알린다** —
		/// 고르는 화면이 생기면 그때 진짜 선택지로 올리면 된다.
		/// </summary>
		private void StartLine(DialogueLine first, Transform speakerTransform)
		{
			DialogueGraph graph = DialogueLineChainGraphBuilder.Build(first, out int skippedBranchCount);
			if (skippedBranchCount > 0)
			{
				Debug.LogWarning(
					$"[DialogueRunner] 옛 대사 사슬에 갈래 {skippedBranchCount}개가 버려진다 — 옛 길은 늘 첫째만 간다: \"{first.Text}\"");
			}

			// 그 자리에서 세운 그래프라 번호가 없다 — 이력에 아무것도 안 남긴다.
			// (기본값 0 을 그대로 쓰면 0 번 자산을 「봤다」고 적는다. 0 은 실제로 쓰이는 번호다.)
			ownsPlayingGraph = true;
			StartGraph(graph, speakerTransform, DataSO.NONE_ID);
		}
	}
}
