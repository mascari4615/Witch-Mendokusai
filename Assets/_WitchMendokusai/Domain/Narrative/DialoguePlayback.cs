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
		private readonly IDialogueEffectSink effectSink;
		private readonly int nodeCount;
		private float waitElapsed;
		private bool stallReported;

		/// <summary>스텝이 바뀔 때마다 — 소비자가 말풍선/선택지 UI 를 갱신하는 자리.</summary>
		public event Action<DialogueStep> OnStepChanged = delegate { };

		/// <summary>대화가 끝났을 때 한 번.</summary>
		public event Action OnFinished = delegate { };

		/// <summary>
		/// 대사에 <see cref="DialogueLine.Wait"/> 가 안 적혀 있을 때 대신 쓸 노출 시간(초).
		/// 0 = 자동 진행 안 함(소비자가 <see cref="Advance"/> 로 넘긴다 — 클릭으로 넘기는 연출).
		/// <see cref="ReadingCharactersPerSecond"/> 가 켜져 있으면 그쪽이 먼저다.
		/// 시간을 상태기 안에 두는 이유: 「몇 초 뒤 넘어가는가」가 화면 없이 검증돼야 하기 때문.
		/// </summary>
		public float DefaultSpeakSeconds { get; set; }

		/// <summary>
		/// 읽는 속도(초당 글자). 0 이하 = 안 씀. 켜면 **대사 길이에 따라** 머무는 시간이 달라진다 —
		/// 「응.」과 두 줄짜리 설명이 같은 시간이면 하나는 지루하고 하나는 다 못 읽는다.
		/// </summary>
		public float ReadingCharactersPerSecond { get; set; }

		/// <summary>읽기 시간의 아래·위 한계(초). 위가 0 이하면 위 한계 없음.</summary>
		public float MinimumSpeakSeconds { get; set; }
		public float MaximumSpeakSeconds { get; set; }

		/// <summary>
		/// 선택지가 떴는데 아무도 안 고르는 채로 이만큼 지나면 <see cref="OnChoiceStalled"/> 를 한 번 울린다.
		/// 0 이하 = 안 씀.
		///
		/// ★ 왜 필요한가: 고르는 쪽(선택지 화면)이 **아직 없다.** 그 상태에서 선택지가 뜨면 대화는
		///   영원히 그 자리에 선다 — 그리고 뒤에 줄 선 대화(퀘스트 보상 대사 등)까지 **전부 막힌다.**
		///   조용히 멈추는 것보다, 크게 알리고 이 대화를 접는 편이 낫다(줄은 계속 흐른다).
		/// </summary>
		public float ChoiceStallSeconds { get; set; }

		/// <summary>선택지가 떴는데 아무도 안 고른다 — 한 번만 울린다.</summary>
		public event Action OnChoiceStalled = delegate { };

		public DialogueStep Current { get; private set; } = DialogueStep.End;

		/// <summary>End 스텝에 닿기 전까지 true. <see cref="Begin"/> 전에는 false.</summary>
		public bool IsPlaying { get; private set; }

		/// <summary>
		/// 끝까지 갔는가. 중간에 <see cref="Stop"/> 으로 접은 것과 구별한다 —
		/// 「이 대화를 끝까지 들었다」를 기록하려면 이 둘이 달라야 한다.
		/// </summary>
		public bool ReachedEnd { get; private set; }

		/// <summary>지금 말하는 줄. Speak 스텝이 아니면 null.</summary>
		public DialogueLine CurrentLine => Current.Kind == DialogueStepKind.Speak ? Current.SpeakLine : null;

		/// <summary>지금 제시된 선택지. Choice 스텝이 아니면 null.</summary>
		public IReadOnlyList<string> CurrentChoices => Current.Kind == DialogueStepKind.Choice ? Current.Options : null;

		/// <param name="effectSink">
		/// 효과 노드를 실제로 일으킬 통로. 그래프에 효과 노드가 있는데 여기가 비어 있으면 터뜨린다
		/// (「주기로 적어 둔 물건이 조용히 안 나오는」 것이 제일 나쁜 결말이라서).
		/// </param>
		public DialoguePlayback(DialogueGraph graph, IDialogueEffectSink effectSink = null)
		{
			traversal = new DialogueGraphTraversal(graph);
			this.effectSink = effectSink;
			nodeCount = graph == null ? 0 : graph.Nodes.Count;
		}

		/// <summary>첫 스텝으로 진입. 빈 그래프면 시작하자마자 끝난다(End 통지 1회).</summary>
		public void Begin()
		{
			IsPlaying = true;
			ReachedEnd = false;
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

		/// <summary>
		/// 고른 답의 **라벨**을 알린다 — 「무엇을 골랐나」를 남기려는 쪽(이력)을 위해.
		///
		/// ★ 왜 이벤트인가: 재생기는 대화 번호를 모른다(그래프만 안다). 번호를 아는 건 부르는 쪽이다.
		///   재생기가 이력에 직접 쓰게 하면 번호를 들려 줘야 하고, 그러면 이 순수 상태기가
		///   저장·이력을 아는 물건이 된다. 「무슨 일이 있었는지」만 말하고 기록은 남에게 맡긴다.
		/// </summary>
		public event Action<string> OnChoiceSelected = delegate { };

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

			// 라벨은 **넘어가기 전에** 집는다 — Apply 뒤엔 Current 가 다음 스텝이라 이미 없다.
			IReadOnlyList<string> options = Current.Options;
			string label = options != null && index >= 0 && index < options.Count ? options[index] : null;

			if (traversal.SelectChoice(index) == false)
			{
				return false;
			}
			if (string.IsNullOrEmpty(label) == false)
			{
				OnChoiceSelected(label);
			}
			Apply(traversal.Next());
			return true;
		}

		/// <summary>
		/// 시간 주입 — Wait(Time) 과 *시간이 정해진 대사* 를 소비한다. 남는 시간은 버리지 않고
		/// 다음 것으로 넘긴다(프레임이 길어도 안 밀린다). 그 외 스텝에서는 아무 일도 안 한다.
		/// </summary>
		public void Tick(float deltaTime)
		{
			if (IsPlaying == false)
			{
				return;
			}

			waitElapsed += deltaTime;

			// 선택지에 선 채로 시간이 흐르면 = 고르는 쪽이 없다는 뜻이다.
			if (Current.Kind == DialogueStepKind.Choice)
			{
				if (stallReported == false && ChoiceStallSeconds > 0f && waitElapsed >= ChoiceStallSeconds)
				{
					stallReported = true;
					OnChoiceStalled();
				}
				return;
			}

			while (IsPlaying && TryGetTimedSeconds(out float seconds) && waitElapsed >= seconds)
			{
				float carry = waitElapsed - seconds;
				Apply(traversal.Next());
				waitElapsed = carry;
			}
		}

		/// <summary>
		/// 지금 스텝이 「시간이 다 되면 저절로 넘어가는」 것인지와 그 초. Wait(Time) 은 노드에 적힌 초,
		/// Speak 는 대사에 적힌 초(없으면 <see cref="DefaultSpeakSeconds"/>, 그것도 0 이면 자동 진행 X).
		/// </summary>
		private bool TryGetTimedSeconds(out float seconds)
		{
			seconds = 0f;

			if (Current.Kind == DialogueStepKind.Wait && Current.WaitKind == DialogueWaitKind.Time)
			{
				seconds = Current.WaitSeconds;
				return true;
			}

			if (Current.Kind != DialogueStepKind.Speak)
			{
				return false;
			}

			DialogueLine line = Current.SpeakLine;
			float lineSeconds = ResolveSpeakSeconds(line);
			if (lineSeconds <= 0f)
			{
				return false;
			}
			seconds = lineSeconds;
			return true;
		}

		/// <summary>
		/// 이 대사가 머무는 시간. 순서: ① 대사에 직접 적힌 시간 ② 읽는 속도로 계산한 시간
		/// ③ 기본값. 셋 다 없으면 0 = 눌러야 넘어간다.
		/// ①이 제일 센 이유: 작가가 「여기서 3초 쉬어」라고 적었으면 그게 연출이다.
		/// </summary>
		private float ResolveSpeakSeconds(DialogueLine line)
		{
			if (line != null && line.Wait > 0f)
			{
				return line.Wait;
			}

			float readingSeconds = DialogueReadingTime.For(
				line == null ? null : line.Text, ReadingCharactersPerSecond, MinimumSpeakSeconds, MaximumSpeakSeconds);
			return readingSeconds > 0f ? readingSeconds : DefaultSpeakSeconds;
		}

		/// <summary>
		/// **건너뛰기** — 사람이 결정하거나 게임이 움직여야 하는 자리가 나올 때까지 죽 넘긴다.
		/// 멈추는 자리는 셋: **선택지 · 사건 대기 · 끝**.
		///
		/// ★ 무엇을 안 건너뛰나가 이 기능의 전부다:
		/// <list type="bullet">
		/// <item>**선택지** — 대신 골라 주면 그건 건너뛰기가 아니라 플레이를 뺏는 것이다.</item>
		/// <item>**사건 대기** — 대화 밖에서 뭔가 일어나야 하는 자리다. 넘기면 대화가 게임을 앞질러
		///   아직 안 열린 문을 열었다고 말한다.</item>
		/// <item>**효과** — 넘기되 **반드시 준다.** 보상·플래그를 건너뛰면 「빨리 봤다」가 「덜 받았다」가 된다.
		///   (넘어가는 길목이 <see cref="Apply"/> 하나뿐이라 저절로 그렇게 된다 — 그게 이 구조의 값이다.)</item>
		/// <item>**시간 대기** — 넘긴다. 그건 연출이고, 건너뛰기는 연출을 접겠다는 뜻이다.</item>
		/// </list>
		///
		/// 「이미 본 대화만 건너뛰게」 같은 정책은 여기 안 넣는다 — 부르는 쪽이 <c>DialogueHistory</c> 를
		/// 보고 정할 일이다. 여기는 「어디서 멈추나」만 안다.
		///
		/// 되돌아가는 고리가 있는 원고에서도 안 멈추면 안 되므로 노드 수만큼만 돈다.
		/// </summary>
		/// <returns>건너뛴 스텝 수. 0 이면 이미 멈출 자리에 서 있었다는 뜻.</returns>
		public int Skip()
		{
			if (IsPlaying == false)
			{
				return 0;
			}

			int skipped = 0;
			int hopBudget = nodeCount;
			while (IsPlaying && IsSkippable(Current))
			{
				if (hopBudget <= 0)
				{
					throw new InvalidOperationException(
						$"Dialogue skip did not reach a stopping point within {nodeCount} hops — the graph loops with nothing to stop at.");
				}
				hopBudget--;
				skipped++;
				Apply(traversal.Next());
			}
			return skipped;
		}

		/// <summary>넘겨도 되는 스텝인가 — 사람도 게임도 개입할 필요가 없는 것만.</summary>
		private static bool IsSkippable(DialogueStep step)
		{
			if (step.Kind == DialogueStepKind.Speak)
			{
				return true;
			}
			return step.Kind == DialogueStepKind.Wait && step.WaitKind == DialogueWaitKind.Time;
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

		/// <summary>
		/// 효과 노드는 소비자에게 안 보인다 — 여기서 일으키고 다음으로 넘긴다(분기와 같은 결).
		/// 효과가 연달아 있어도 스텝 하나 안에서 다 처리한다. 손편집 자산이 효과끼리 고리를 이루면
		/// 노드 수만큼만 돌고 터뜨린다.
		/// </summary>
		private DialogueStep ApplyEffectsUntilVisibleStep(DialogueStep step)
		{
			int hopBudget = nodeCount;
			while (step.Kind == DialogueStepKind.Effect)
			{
				if (hopBudget <= 0)
				{
					throw new InvalidOperationException(
						$"DialogueEffectNode chain did not terminate within {nodeCount} hops — effect nodes form a loop.");
				}
				hopBudget--;

				if (effectSink == null)
				{
					throw new InvalidOperationException(
						"Dialogue graph has a DialogueEffectNode but no effect sink was given — the authored effects would silently do nothing.");
				}
				if (step.Effects != null && step.Effects.Count > 0)
				{
					effectSink.Apply(step.Effects);
				}
				if (step.EffectData != null && step.EffectData.Count > 0)
				{
					effectSink.ApplyData(step.EffectData);
				}
				step = traversal.Next();
			}
			return step;
		}

		private void Apply(DialogueStep step)
		{
			step = ApplyEffectsUntilVisibleStep(step);

			Current = step;
			waitElapsed = 0f;
			stallReported = false;
			OnStepChanged(step);

			if (step.Kind != DialogueStepKind.End)
			{
				return;
			}
			IsPlaying = false;
			ReachedEnd = true;
			OnFinished();
		}
	}
}
