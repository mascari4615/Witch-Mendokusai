using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 노드 런타임 비주얼 + 인터랙션 Provider — 도메인별 노드 타입에 커스텀 VisualElement 와 클릭/hover 행동 주입.
	/// `[NodeRuntimeView(typeof(MyNode))]` attribute 로 등록 → <see cref="NodeRuntimeProviderRegistry"/> 가 reflection 으로 카탈로그.
	/// 미등록 노드 타입은 <see cref="DefaultNodeRuntimeViewProvider"/> fallback (라벨만, body 비움, 인터랙션 no-op).
	///
	/// H3 (2026-05-09): Build 노출 — 도메인별 비주얼.
	/// H4 (2026-05-09): OnClicked / OnHovered / OnUnhovered default no-op 추가 — 도메인이 인터랙션도 책임 (단일 책임).
	///                    framework 의 cross-cutting event 는 <see cref="NodeGraphRuntimeView.OnNodeClicked"/> 별도 (디버그/분석/로깅).
	/// </summary>
	public interface INodeRuntimeViewProvider
	{
		/// <summary>노드 body 안에 들어갈 VisualElement 생성. null 반환 시 body 비움 (라벨만).</summary>
		VisualElement Build(NodeBase node);

		/// <summary>host 가 필요한 도메인용. host 는 <see cref="NodeGraphRuntimeView.Host"/> 에 화면 주인이 꽂은 것 (예: QuestManager). default 는 host 무시</summary>
		VisualElement Build(NodeBase node, object host) => Build(node);

		/// <summary>노드 좌클릭 시 도메인 행동. default = no-op. 예: MagicBook QuestProvider 가 퀘스트 상세 패널 open.</summary>
		void OnClicked(NodeBase node) { }

		/// <summary>노드 hover 시작 시 도메인 행동. default = no-op. 예: 툴팁 표시.</summary>
		void OnHovered(NodeBase node) { }

		/// <summary>노드 hover 해제 시 도메인 행동. default = no-op. 예: 툴팁 숨김.</summary>
		void OnUnhovered(NodeBase node) { }
	}
}
