using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화 재생 상태기 (TASK-WM-052 Phase 2 — 그래프를 *실제로 쓰는 첫 소비자*).
	///
	/// ★ 왜 필요한가: <see cref="DialogueGraphTraversal"/> 이 Speak/Choice/Wait/Branch 를 다 걷는데도
	///   <see cref="DialogueRunner"/> 는 여전히 옛 <see cref="DialogueLine.Choices"/> 사슬을 걸었다 =
	///   그래프 substrate 의 호출처가 **0곳**이었다(「데드 인터페이스 금지」 위반). 이 클래스가 그 고리를 잇는다.
	///
	/// ★ 왜 MonoBehaviour 가 아닌가: 시간·코루틴·말풍선을 섞으면 검증이 PlayMode 로 올라가 버린다.
	///   여기는 *결정적 순수 로직* 만 — 시간은 <see cref="Tick"/> 로 밖에서 주입받고, 이벤트는
	///   <see cref="NotifyEvent"/> 로 받는다(traversal 이 SelectChoice 를 밖에서 받는 것과 같은 결).
	///   Unity 쪽(러너)은 말풍선·연출·Time.deltaTime 전달만 하는 얇은 껍데기가 된다.
	///
	/// 소비자 계약:
	/// <list type="bullet">
	/// <item>Speak — 소비자가 대사를 다 보여준 뒤 <see cref="Advance"/>(대사 길이는 연출 쪽 결정).</item>
	/// <item>Choice — <see cref="SubmitChoice"/> 로 고르면 그 자리에서 다음 스텝으로 넘어간다.</item>
	/// <item>Wait(Time) — <see cref="Tick"/> 누적이 초를 넘으면 자동 진행.</item>
	/// <item>Wait(Event) — 같은 id 로 <see cref="NotifyEvent"/> 가 오면 진행. 다른 id 는 무시.</item>
	/// <item>Branch — 소비자가 볼 일 없다(traversal 이 스텝 전에 삼킨다).</item>
	/// </list>
	/// </summary>
	public sealed class DialoguePlayback
	{
		private readonly DialogueGraphTraversal traversal;
		private float waitElapsed;

		/// <summary>스텝이 바뀔 때마다 — 소비자가 말풍선/선택지 UI 를 갱신하는 자리.</summary>
		public event Action<DialogueStep> OnStepChanged = delegate { };

		/// <summary>대화가 끝났을 때 한 번.</summary>
		public event Action OnFinished = delegate { };

		public DialogueStep Current { get; private set; } = DialogueStep.End;

		/// <summary>End 스텝에 닿기 전까지 true. <see cref="Begin"/> 전에는 false.</summary>
		public bool IsPlaying { get; private set; }

		/// <summary>지금 말하는 줄. Speak 스텝이 아니면 null.</summary>
		public DialogueLine CurrentLine => Current.Kind == DialogueStepKind.Speak ? Current.SpeakLine : null;

		/// <summary>지금 제시된 선택지. Choice 스텝이 아니면 null.</summary>
		public IReadOnlyList<string> CurrentChoices => Current.Kind == DialogueStepKind.Choice ? Current.Options : null;

		public DialoguePlayback(DialogueGraph graph)
		{
			traversal = new DialogueGraphTraversal(graph);
		}

		/// <summary>첫 스텝으로 진입. 빈 그래프면 시작하자마자 끝난다(End 통지 1회).</summary>
		public void Begin()
		{
			IsPlaying = true;
			Apply(traversal.Start());
		}

		/// <summary>
		/// Speak 를 다 보여준 뒤 다음으로. Wait 스텝은 시간/이벤트가 주인이라 여기서 안 넘긴다
		/// (넘겨 주면 「기다리라고 적어둔 것」이 무의미해진다). Choice 는 고른 뒤에만 넘어간다.
		/// </summary>
		public void Advance()
		{
			if (IsPlaying == false)
			{
				return;
			}
			if (Current.Kind == DialogueStepKind.Wait)
			{
				return;
			}
			if (Current.Kind == DialogueStepKind.Choice)
			{
				return;
			}
			Apply(traversal.Next());
		}

		/// <summary>i 번째 선택지를 고르고 그 가지로 진행. Choice 스텝이 아니거나 범위 밖이면 false(상태 불변).</summary>
		public bool SubmitChoice(int index)
		{
			if (IsPlaying == false)
			{
				return false;
			}
			if (Current.Kind != DialogueStepKind.Choice)
			{
				return false;
			}
			if (traversal.SelectChoice(index) == false)
			{
				return false;
			}
			Apply(traversal.Next());
			return true;
		}

		/// <summary>시간 주입 — Wait(Time) 만 소비한다. 남는 시간은 버리지 않고 다음 대기로 넘긴다.</summary>
		public void Tick(float deltaTime)
		{
			if (IsPlaying == false)
			{
				return;
			}
			if (Current.Kind != DialogueStepKind.Wait || Current.WaitKind != DialogueWaitKind.Time)
			{
				return;
			}

			waitElapsed += deltaTime;
			while (IsPlaying
				&& Current.Kind == DialogueStepKind.Wait
				&& Current.WaitKind == DialogueWaitKind.Time
				&& waitElapsed >= Current.WaitSeconds)
			{
				float carry = waitElapsed - Current.WaitSeconds;
				Apply(traversal.Next());
				waitElapsed = carry;
			}
		}

		/// <summary>바깥 사건 통지 — 기다리던 id 와 같을 때만 진행.</summary>
		public void NotifyEvent(string eventId)
		{
			if (IsPlaying == false)
			{
				return;
			}
			if (Current.Kind != DialogueStepKind.Wait || Current.WaitKind != DialogueWaitKind.Event)
			{
				return;
			}
			if (string.Equals(Current.WaitEventId, eventId, StringComparison.Ordinal) == false)
			{
				return;
			}
			Apply(traversal.Next());
		}

		/// <summary>중단 — 남은 그래프를 버린다. 끝 통지는 정상 종료와 같게 한 번 나간다.</summary>
		public void Stop()
		{
			if (IsPlaying == false)
			{
				return;
			}
			IsPlaying = false;
			Current = DialogueStep.End;
			OnStepChanged(Current);
			OnFinished();
		}

		private void Apply(DialogueStep step)
		{
			Current = step;
			waitElapsed = 0f;
			OnStepChanged(step);

			if (step.Kind != DialogueStepKind.End)
			{
				return;
			}
			IsPlaying = false;
			OnFinished();
		}
	}
}
