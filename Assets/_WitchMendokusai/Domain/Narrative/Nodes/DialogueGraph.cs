using WitchMendokusai.NodeGraph;
using UnityEngine;
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화/스토리 시퀀스 그래프 — <see cref="NodeGraph"/> 직접 상속 (ChapterSO 와 동일 패턴, TASK-WM-052 Phase 2).
	/// 「데이터 = 그래프」 단일 정본: nodes(base 의 [SerializeReference]) 안에 <see cref="DialogueStartNode"/> /
	/// <see cref="DialogueSpeakNode"/> 등 플로우 노드가 들어가고, connections(Flow 엣지)가 진행 순서/분기.
	///
	/// Pull executor(<see cref="NodeExecutionContext"/>) 사용 X — 대화는 *순서 traversal* 이라
	/// <see cref="DialogueGraphTraversal"/> 이 연결을 직접 따라간다(QuestNode 의 "Pull 사용 X" 선례 일관).
	/// 에디터 GraphView / <see cref="NodeGraphValidator"/> / 직렬화 substrate 는 그대로 재사용 —
	/// foundation 이 2번째 도메인(지형 다음 대화)으로 일반화됨을 증명하는 것이 본 단계의 목적.
	/// </summary>
	[CreateAssetMenu(fileName = "DialogueGraph_", menuName = "WM/Narrative/DialogueGraph")]
	public class DialogueGraph : NodeGraphAsset
	{
		public override NodeDomain Domain => NodeDomain.Dialogue;
	}
}
