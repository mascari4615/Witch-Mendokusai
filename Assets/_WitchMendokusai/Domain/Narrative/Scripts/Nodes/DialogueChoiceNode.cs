using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 선택지 분기 노드 (TASK-WM-052 Phase 2 #6). 입력 플로우 `in` + 선택지 수만큼 출력
	/// 플로우 `choice0`..`choice{N-1}`. traversal 이 이 노드 도달 시 Choice 스텝(prompt +
	/// 옵션 라벨) 방출 → 소비자가 <see cref="DialogueGraphTraversal.SelectChoice"/> 로 i 선택 →
	/// `choice{i}` 연결을 따라 분기. 어느 포트도 연결 안 됐으면 그 분기 = 대화 종료.
	///
	/// Pull executor 사용 X(QuestNode/Speak 선례) — <see cref="OnEvaluate"/> 무동작.
	/// prompt/옵션 라벨 = 디자이너 노출(수치/문자열 노출 룰). 옵션별 결과는 *연결*이 정본
	/// (DialogueLine 중복 보유 X — Speak 노드가 분기 끝에서 말함). Options setter =
	/// DialogueSpeakNode.Line / QuestNode.Target 선례 일관(런타임·테스트 구성).
	/// </summary>
	[Serializable]
	public class DialogueChoiceNode : NodeBase
	{
		public const string PORT_IN = "in";
		private const string PORT_CHOICE_PREFIX = "choice";

		[SerializeField, TextArea] private string prompt;
		[SerializeField] private List<string> options = new();

		public string Prompt { get => prompt; set => prompt = value; }
		public List<string> Options { get => options; set => options = value ?? new(); }

		/// <summary>i 번째 선택지의 출력 포트 id — traversal 분기 + 에디터 연결이 공유하는 안정 식별.</summary>
		public static string ChoicePortId(int index) => PORT_CHOICE_PREFIX + index;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			yield return new NodePort<FlowSignal>(this, PORT_IN, PortDirection.Input);
			for (int i = 0; i < options.Count; i++)
			{
				yield return new NodePort<FlowSignal>(this, ChoicePortId(i), PortDirection.Output);
			}
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
		}
	}
}
