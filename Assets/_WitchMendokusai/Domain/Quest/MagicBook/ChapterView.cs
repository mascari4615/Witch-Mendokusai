using UnityEngine.UIElements;
using WitchMendokusai.NodeGraph.Runtime;
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 단일 챕터의 노드 그래프 시각 (UI Toolkit). <see cref="ChapterSO"/> 가 NodeGraph 직접 상속이라
	/// <see cref="NodeGraphRuntimeView"/> 에 그대로 Bind. QuestNode wrapper 들이 nodes List 에 들어있고,
	/// QuestNodeRuntimeViewProvider 가 비주얼 (Sprite + Name) + 클릭 행동 책임 (TASK-WM-034 H Provider 패턴).
	///
	/// TASK-WM-059 C (2026-05-09) — uGUI UIChapter 의 UI Toolkit 대체. 노드 위치 (NodeBase.EditorPosition) +
	/// UnlockQuest effect → edge (TODO: B 단계는 데이터 모델만, edge 자동 도출은 후속 단계).
	/// </summary>
	public class ChapterView : VisualElement
	{
		public const string USS_CLASS = "wm-chapter-view";

		private readonly NodeGraphRuntimeView graphView;
		private ChapterSO boundChapter;

		public ChapterSO BoundChapter => boundChapter;
		public NodeGraphRuntimeView GraphView => graphView;

		public ChapterView()
		{
			AddToClassList(USS_CLASS);
			style.flexGrow = 1;

			graphView = new NodeGraphRuntimeView();
			graphView.style.flexGrow = 1;
			Add(graphView);
		}

		public void Bind(ChapterSO chapter)
		{
			boundChapter = chapter;
			graphView.Bind(chapter);
		}
	}
}
