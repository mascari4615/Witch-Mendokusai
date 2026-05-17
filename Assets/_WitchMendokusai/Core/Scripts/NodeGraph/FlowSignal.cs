namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 플로우(제어 흐름) 엣지 마커 타입. 데이터를 나르지 않고 "여기 다음 저기" 라는 *순서/분기* 만 표현.
	///
	/// 노드 그래프 foundation 은 Pull 데이터플로우(`NodePort&lt;float&gt;` 등) 와 *플로우 traversal*
	/// (Dialogue / Cutscene / Quest cascade) 을 같은 substrate(NodeGraph SO + NodeBase + 에디터
	/// GraphView + `NodeGraphValidator`) 위에서 공유한다. 플로우 노드는 `NodePort&lt;FlowSignal&gt;`
	/// 포트를 선언 → 검증기/GraphView 가 *다른 타입처럼* 취급(Flow↔Flow 만 연결, 색 구분).
	/// 실행은 Pull(`NodeExecutionContext`) 이 아니라 도메인 traversal(예: `DialogueGraphTraversal`)
	/// 이 연결을 직접 따라감 — `ChapterSO`/`QuestNode` 가 이미 검증한 "substrate 공유, executor 는
	/// 도메인별" 패턴(TASK-WM-051 B / WM-052 Phase 2).
	///
	/// `readonly struct` + 무필드 = 값/할당 비용 0, `typeof(FlowSignal)` 만 식별자로 쓰임.
	/// </summary>
	public readonly struct FlowSignal
	{
	}
}
