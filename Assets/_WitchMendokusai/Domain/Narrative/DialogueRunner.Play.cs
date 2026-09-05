using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// DialogueRunner 의 재생 흐름 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 DialogueRunner.cs 를 본다.
	public partial class DialogueRunner : MonoBehaviour
	{
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
