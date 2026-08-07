using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 「지금 걸까, 줄을 세울까, 다음 걸 이어 걸까」만 정하는 조정자 (TASK-WM-052).
	///
	/// ★ 왜 따로 뽑았나: 이 판단이 <see cref="DialogueRunner"/>(MonoBehaviour) 안에 있으면
	///   **실제로 게임을 켜야만 확인된다.** 앞 바퀴에 「이어 거는 흐름은 in-Play 확인이 남는다」고
	///   적어 둔 자리가 정확히 여기였다. 판단만 밖으로 빼면 화면 없이 잠글 수 있다.
	///
	/// 러너는 이제 「어떻게 거는가」(그래프 세우기·코루틴·말풍선)만 맡고,
	/// 「언제 거는가」는 여기서 정한다.
	/// </summary>
	public sealed class DialoguePlayCoordinator
	{
		private readonly DialoguePlayQueue queue;

		/// <summary>실제로 걸어 달라 — 러너가 받아 재생을 시작한다.</summary>
		public event Action<DialoguePlayRequest> OnStartRequested = delegate { };

		public DialoguePlayCoordinator(DialoguePlayQueue playQueue = null)
		{
			queue = playQueue ?? new DialoguePlayQueue();
		}

		/// <summary>지금 뭔가 말하는 중인가.</summary>
		public bool IsBusy { get; private set; }

		/// <summary>기다리는 개수(지금 말하는 것 제외).</summary>
		public int PendingCount => queue.Count;

		/// <summary>
		/// 재생 요청. 비어 있으면 바로 걸고, 말하는 중이면 줄을 세운다.
		/// 줄에 못 넣었으면(중복·꽉 참) false — 부르는 쪽이 알린다.
		/// </summary>
		public bool Request(DialoguePlayRequest request)
		{
			if (request.IsEmpty)
			{
				return false;
			}

			if (IsBusy)
			{
				return queue.Enqueue(request);
			}

			IsBusy = true;
			RaiseStart(request);
			return true;
		}

		/// <summary>
		/// 「걸어라」를 알린다 — **듣는 쪽이 없으면 터뜨린다.**
		///
		/// ★ 왜 조용히 넘기지 않나: 듣는 쪽이 없으면 이 대화는 **그냥 사라진다.**
		///   그런데 바쁨 표시는 켜졌으니 그 뒤에 온 대화는 전부 줄에서 기다리다 같이 묻힌다 —
		///   퀘스트 보상 대사도, 다음 장면도. 화면엔 아무 일도 안 일어난 것처럼 보인다.
		///
		///   실제로 이렇게 될 수 있다: 러너가 구독을 붙이는 자리가 유니티 수명주기(Awake)라,
		///   **다른 컴포넌트가 자기 Awake 에서 대화를 걸면** 순서에 따라 아직 안 붙어 있다.
		///   그 순서는 씬마다 다르고, 안 터지면 아무도 못 찾는다.
		///
		///   효과 통로가 없을 때 터뜨리는 것과 같은 판단 —
		///   「하기로 적어 둔 것이 조용히 안 되는 것」이 제일 나쁜 결말이다.
		/// </summary>
		private void RaiseStart(DialoguePlayRequest request)
		{
			// 초기값 `delegate { }` 가 하나 들어 있으므로, 진짜 구독자는 그 다음부터다.
			if (OnStartRequested.GetInvocationList().Length <= 1)
			{
				throw new InvalidOperationException(
					"DialoguePlayCoordinator was asked to start a dialogue but nothing is listening to OnStartRequested — "
					+ "the dialogue (and everything queued behind it) would vanish silently.");
			}
			OnStartRequested(request);
		}

		/// <summary>
		/// 지금 것이 끝났다. 기다리던 게 있으면 이어 걸고, 없으면 조용해진다.
		/// 끝나지도 않았는데 부르면(이미 안 바쁨) 아무 일도 안 한다.
		/// </summary>
		public void NotifyFinished()
		{
			if (IsBusy == false)
			{
				return;
			}

			if (queue.TryDequeue(out DialoguePlayRequest next) == false)
			{
				IsBusy = false;
				return;
			}

			// 바쁨을 유지한 채 다음 것을 건다 — 중간에 잠깐 「안 바쁨」이 되면
			// 그 틈에 들어온 요청이 줄을 건너뛰어 순서가 뒤집힌다.
			RaiseStart(next);
		}

		/// <summary>전부 접는다 — 「지금 대화 그만」이면 기다리던 것도 같이 버린다.</summary>
		public void Reset()
		{
			queue.Clear();
			IsBusy = false;
		}
	}
}
