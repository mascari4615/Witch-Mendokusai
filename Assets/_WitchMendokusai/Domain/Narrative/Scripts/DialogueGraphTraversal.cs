using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	public enum DialogueStepKind
	{
		Speak,
		Choice,
		Wait,
		Effect,
		End,
	}

	/// <summary>
	/// traversal 이 방출하는 한 스텝.
	/// Speak → <see cref="SpeakLine"/> 유효. Choice → <see cref="Prompt"/> + <see cref="Options"/>
	/// 유효(소비자는 <see cref="DialogueGraphTraversal.SelectChoice"/> 로 분기 선택).
	/// Effect → <see cref="Effects"/> 유효(소비자가 적용 후 <see cref="DialogueGraphTraversal.Next"/>).
	/// Wait → <see cref="WaitKind"/> + <see cref="WaitSeconds"/> + <see cref="WaitEventId"/> 유효
	/// (소비자가 시간/이벤트 만족 후 <see cref="DialogueGraphTraversal.Next"/> = 대기 완료 신호).
	/// End → 전부 기본값.
	/// </summary>
	public readonly struct DialogueStep
	{
		public DialogueStepKind Kind { get; }
		public DialogueLine SpeakLine { get; }
		public string Prompt { get; }
		public IReadOnlyList<string> Options { get; }
		public DialogueWaitKind WaitKind { get; }
		public float WaitSeconds { get; }
		public string WaitEventId { get; }
		public IReadOnlyList<EffectInfo> Effects { get; }
		public IReadOnlyList<EffectInfoData> EffectData { get; }

		private DialogueStep(DialogueStepKind kind, DialogueLine speakLine, string prompt, IReadOnlyList<string> options,
			DialogueWaitKind waitKind, float waitSeconds, string waitEventId, IReadOnlyList<EffectInfo> effects = null,
			IReadOnlyList<EffectInfoData> effectData = null)
		{
			Effects = effects;
			EffectData = effectData;
			Kind = kind;
			SpeakLine = speakLine;
			Prompt = prompt;
			Options = options;
			WaitKind = waitKind;
			WaitSeconds = waitSeconds;
			WaitEventId = waitEventId;
		}

		public static DialogueStep Effect(IReadOnlyList<EffectInfo> effects, IReadOnlyList<EffectInfoData> effectData = null) =>
			new(DialogueStepKind.Effect, null, null, null, default, 0f, null, effects, effectData);
		public static DialogueStep Speak(DialogueLine line) =>
			new(DialogueStepKind.Speak, line, null, null, default, 0f, null);
		public static DialogueStep Choice(string prompt, IReadOnlyList<string> options) =>
			new(DialogueStepKind.Choice, null, prompt, options, default, 0f, null);
		public static DialogueStep Wait(DialogueWaitKind waitKind, float waitSeconds, string waitEventId) =>
			new(DialogueStepKind.Wait, null, null, null, waitKind, waitSeconds, waitEventId);
		public static readonly DialogueStep End = new(DialogueStepKind.End, null, null, null, default, 0f, null);
	}

	/// <summary>
	/// 대화 그래프의 *순수* 플로우 traversal — Unity I/O(코루틴/버블/사운드) 0, 결정적.
	/// 그래서 EditMode 에서 그대로 회귀 잠금 가능(황금의 정신 「피드백 루프 먼저」 = testable seam).
	/// MonoBehaviour 러너(DialogueRunner Phase 2 통합 — 다음 단계)는 이 스텝 시퀀스를 소비해
	/// 버블/typewriter/sfx 연출만 담당(traversal 로직과 분리).
	///
	/// 현 단계: <see cref="DialogueStartNode"/> → (<see cref="DialogueSpeakNode"/> |
	/// <see cref="DialogueChoiceNode"/>)* → 종료. #6 Choice 추가 시 코어 "현재 노드의 출력
	/// 포트 → 연결 → 타깃"(<see cref="FollowFlow"/>) 는 *불변* — 따라갈 출력 포트 id 만
	/// 노드 타입별로 분기(<see cref="OutputPortToFollow"/>). Wait/Branch 도 동일 패턴 확장.
	///
	/// <see cref="DialogueBranchNode"/> 는 *스텝을 방출하지 않는* 유일한 노드다 — 게임 상태가
	/// 고르는 분기라 플레이어에게 보여줄 것이 없다. 그래서 traversal 이 도달 즉시 조건을 평가하고
	/// 다음 노드로 건너뛴다(<see cref="ResolveBranchChain"/>). 소비자(DialogueRunner)는 분기의
	/// 존재를 모른 채 Speak/Choice/Wait/End 만 계속 받는다 = 연출 코드 변경 0.
	/// </summary>
	public sealed class DialogueGraphTraversal
	{
		private readonly DialogueGraph graph;
		private NodeBase currentNode;
		private int pendingChoice = -1;

		// 화면에 보이는 순번 → 노드에 적힌 순번. 조건이 안 맞아 빠진 칸이 있으면 둘이 어긋나는데,
		// 포트는 *적힌 순번* 으로 고정이라 이 표가 없으면 엉뚱한 가지로 간다.
		private readonly List<int> visibleToAuthored = new();

		public DialogueGraphTraversal(DialogueGraph graph)
		{
			this.graph = graph;
		}

		/// <summary>진입점 노드에서 첫 플로우 엣지를 따라 첫 스텝 반환. Start 노드/연결 없으면 End.</summary>
		public DialogueStep Start()
		{
			if (graph == null)
			{
				return DialogueStep.End;
			}

			DialogueStartNode startNode = FindStartNode();
			if (startNode == null)
			{
				return DialogueStep.End;
			}

			currentNode = FollowFlow(startNode, DialogueStartNode.PORT_NEXT);
			return StepForCurrent();
		}

		/// <summary>
		/// 현재 노드의 진행 플로우 엣지를 따라 다음 스텝. Speak→`next` / Choice→선택된
		/// `choice{i}`(미선택이면 End). 연결 없거나 끝이면 End.
		/// </summary>
		public DialogueStep Next()
		{
			if (currentNode == null)
			{
				return DialogueStep.End;
			}

			string outputPortId = OutputPortToFollow(currentNode);
			if (outputPortId == null)
			{
				return DialogueStep.End;
			}

			currentNode = FollowFlow(currentNode, outputPortId);
			return StepForCurrent();
		}

		/// <summary>
		/// Choice 스텝에서 분기 선택 — i 번째 옵션 포트(`choice{i}`)를 다음 <see cref="Next"/> 가 따라감.
		/// 현재 노드가 Choice 아니거나 index 범위 밖이면 false(상태 불변).
		/// </summary>
		public bool SelectChoice(int index)
		{
			if (currentNode is DialogueChoiceNode == false)
			{
				return false;
			}
			if (index < 0 || index >= visibleToAuthored.Count)
			{
				return false;
			}
			pendingChoice = visibleToAuthored[index];
			return true;
		}

		/// <summary>노드 타입별 진행 출력 포트 id. 미선택 Choice / 미지원 타입 = null(=End).</summary>
		private string OutputPortToFollow(NodeBase node)
		{
			if (node is DialogueSpeakNode)
			{
				return DialogueSpeakNode.PORT_NEXT;
			}
			if (node is DialogueChoiceNode)
			{
				return pendingChoice >= 0 ? DialogueChoiceNode.ChoicePortId(pendingChoice) : null;
			}
			if (node is DialogueWaitNode)
			{
				return DialogueWaitNode.PORT_NEXT;
			}
			if (node is DialogueEffectNode)
			{
				return DialogueEffectNode.PORT_NEXT;
			}
			return null;
		}

		/// <summary>
		/// 현재 노드가 분기면 조건을 평가해 통과 포트로 계속 건너뛴다 — 분기가 연달아 있어도
		/// 스텝 하나 안에서 다 해소된다. 분기끼리 고리를 이루면(A→B→A) 영원히 돌 수 있으므로
		/// 노드 수만큼만 건너뛰고 그 이상은 **터뜨린다**(조용히 대화를 끝내면 원인이 안 보인다).
		/// </summary>
		private void ResolveBranchChain()
		{
			int hopBudget = graph == null ? 0 : graph.Nodes.Count;
			while (currentNode is DialogueBranchNode branchNode)
			{
				if (hopBudget <= 0)
				{
					throw new InvalidOperationException(
						$"DialogueBranchNode chain did not terminate within {graph.Nodes.Count} hops — branch nodes form a loop (last: {branchNode.Id}).");
				}
				hopBudget--;

				bool conditionMet = EvaluateBranch(branchNode);
				currentNode = FollowFlow(branchNode, conditionMet ? DialogueBranchNode.PORT_TRUE : DialogueBranchNode.PORT_FALSE);
			}
		}

		/// <summary>
		/// 분기 조건 평가. 조건 미할당 = *데이터 오류* 라 기본값으로 덮지 않고 터뜨린다
		/// (FastFail — 어느 쪽으로 흘려보내도 "왜 이 대사가 나왔지" 를 못 되짚는다).
		/// </summary>
		private bool EvaluateBranch(DialogueBranchNode branchNode)
		{
			if (branchNode.Condition == null)
			{
				throw new InvalidOperationException(
					$"DialogueBranchNode '{branchNode.Id}' has no Condition assigned — cannot decide a branch.");
			}
			return branchNode.Condition.Evaluate();
		}

		private DialogueStep StepForCurrent()
		{
			ResolveBranchChain();

			if (currentNode is DialogueSpeakNode speakNode)
			{
				return DialogueStep.Speak(speakNode.Line);
			}
			if (currentNode is DialogueChoiceNode choiceNode)
			{
				pendingChoice = -1;
				List<string> availableLabels = BuildAvailableOptions(choiceNode);

				// 고를 수 있는 칸이 하나도 없으면 선택지를 띄울 수 없다 — 빈 목록을 보여 주고 멈추는 대신
				// 대화를 끝낸다(연결 안 된 가지와 같은 결말). 이 상황은 검사기가 미리 경고한다.
				if (availableLabels.Count == 0)
				{
					currentNode = null;
					return DialogueStep.End;
				}

				return DialogueStep.Choice(choiceNode.Prompt, availableLabels);
			}
			if (currentNode is DialogueWaitNode waitNode)
			{
				return DialogueStep.Wait(waitNode.Kind, waitNode.Seconds, waitNode.EventId);
			}
			if (currentNode is DialogueEffectNode effectNode)
			{
				return DialogueStep.Effect(effectNode.Effects, effectNode.EffectData);
			}
			return DialogueStep.End;
		}

		/// <summary>
		/// 조건이 맞는 칸의 라벨만 모으고, 그 순번 → 적힌 순번 표를 다시 만든다.
		/// 조건 평가는 *선택지를 띄우는 그 순간* 한 번 — 띄운 뒤 상황이 바뀌어도 목록은 안 흔들린다
		/// (고르는 중에 항목이 사라지면 손가락이 엉뚱한 걸 누른다).
		/// </summary>
		private List<string> BuildAvailableOptions(DialogueChoiceNode choiceNode)
		{
			visibleToAuthored.Clear();
			List<string> labels = new();

			List<DialogueChoiceOption> options = choiceNode.Options;
			for (int i = 0; i < options.Count; i++)
			{
				DialogueChoiceOption option = options[i];
				if (option == null || option.IsAvailable() == false)
				{
					continue;
				}
				visibleToAuthored.Add(i);
				labels.Add(option.Label);
			}
			return labels;
		}

		private DialogueStartNode FindStartNode()
		{
			IReadOnlyList<NodeBase> nodes = graph.Nodes;
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i] is DialogueStartNode startNode)
				{
					return startNode;
				}
			}
			return null;
		}

		/// <summary>node 의 outputPortId 출력 포트에서 나가는 Flow 연결의 타깃 노드. 없으면 null.</summary>
		private NodeBase FollowFlow(NodeBase node, string outputPortId)
		{
			if (node == null)
			{
				return null;
			}

			IReadOnlyList<NodeConnection> connections = graph.Connections;
			for (int i = 0; i < connections.Count; i++)
			{
				NodeConnection connection = connections[i];
				if (connection == null)
				{
					continue;
				}
				if (connection.SourceNodeId == node.Id && connection.SourcePortId == outputPortId)
				{
					return graph.FindNode(connection.TargetNodeId);
				}
			}
			return null;
		}
	}
}
