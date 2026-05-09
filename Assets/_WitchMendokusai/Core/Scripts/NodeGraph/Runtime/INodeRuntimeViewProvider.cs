using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 노드 런타임 비주얼 Provider — 도메인별 노드 타입에 커스텀 VisualElement 주입.
	/// `[NodeRuntimeView(typeof(MyNode))]` attribute 로 등록 → <see cref="NodeRuntimeProviderRegistry"/> 가 reflection 으로 카탈로그.
	/// 미등록 노드 타입은 <see cref="DefaultNodeRuntimeViewProvider"/> fallback (라벨만, body 비움).
	///
	/// H3 (2026-05-09): Build 만 노출. 부분 갱신 (Refresh) 은 후속 단계에서 사용처 발생 시 추가 — 데드 hook 방지.
	/// 현재 그래프 데이터 변경 시 갱신은 <see cref="NodeGraphRuntimeView.Refresh"/> 의 full rebuild 로 충분.
	/// </summary>
	public interface INodeRuntimeViewProvider
	{
		/// <summary>노드 body 안에 들어갈 VisualElement 생성. null 반환 시 body 비움 (라벨만).</summary>
		VisualElement Build(NodeBase node);
	}
}
