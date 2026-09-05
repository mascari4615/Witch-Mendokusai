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
	public partial class DialogueRunner : MonoBehaviour
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

		// 같은 씬에 러너가 둘일 때 「진 쪽」. 진 쪽은 자기를 지우는데, 지우면서 창구까지 지우면
		// **이긴 쪽이 쓰던 창구가 빈다** — 그 뒤로 물건·의뢰 조건이 전부 「없다」로 넘어진다.
		private bool lostToAnotherRunner;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				lostToAnotherRunner = true;
				RetireAsDuplicate();
				return;
			}
			Instance = this;
			DialogueHistoryBridge.Register(History);
			EnsureCoordinatorWired();
		}

		/// <summary>
		/// 진 쪽이 물러난다 — 놀 때는 스스로 사라지고, 편집 중에는 말로만 알린다.
		///
		/// ★ 왜 두 갈래인가: 편집 중(플레이 아님)에는 <c>Destroy</c> 가 **아무것도 안 하고 에러만 남긴다.**
		///   러너를 실수로 둘 둔 채 씬을 만지는 동안 진 쪽은 그대로 있고 콘솔만 빨개진다 —
		///   고치라는 신호가 「고칠 수 없는 에러」로 온다.
		///
		/// ★ 왜 편집 중엔 안 지우나: 사람이 방금 놓은 오브젝트를 말없이 지우는 건 편집 도구가 할 일이 아니다.
		///   놀기 시작하면 어차피 하나만 남으므로, 편집 중에는 **알리고 두는 것**이 맞다.
		/// </summary>
		private void RetireAsDuplicate()
		{
			if (Application.isPlaying)
			{
				Destroy(gameObject);
				return;
			}

			Debug.LogWarning($"{name}: 대화 러너가 둘이다 — 놀기 시작하면 나중 것이 스스로 물러난다. 씬에서 하나로 정리해 두는 게 좋다.");
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

			// 진 쪽이 사라지면서 창구를 비웠을 수 있다 — 주입이 진 쪽에 먼저 닿았으면 그쪽 것이 등록돼 있다.
			// 이긴 쪽이 자기 것을 다시 걸어 둔다. 안 하면 조건이 전부 「없다」로 넘어진 채 게임이 돈다.
			if (lostToAnotherRunner && Instance != null && Instance != this)
			{
				Instance.ReassertBridges();
			}
		}

		/// <summary>자기 창구를 다시 건다 — 남이 지우고 간 뒤 이긴 쪽이 부른다.</summary>
		private void ReassertBridges()
		{
			DialogueHistoryBridge.Register(History);
			if (itemCountSource != null)
			{
				DialogueItemBridge.Register(itemCountSource);
			}
			if (questStateSource != null)
			{
				DialogueQuestBridge.Register(questStateSource);
			}
		}

		private DialoguePlayback playback;
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

		/// <summary>지금 틀고 있는 대화의 번호. 번호 없는 재생이면 <see cref="DataSO.NONE_ID"/>.</summary>
		private int CurrentDialogueId => playingDialogueId;
	}
}
