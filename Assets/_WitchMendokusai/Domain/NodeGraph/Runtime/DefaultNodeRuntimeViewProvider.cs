using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// Generic fallback Provider — 모든 미등록 노드 타입에 사용. body 안에 아무것도 안 넣음 (타이틀 라벨만 보임).
	/// <see cref="NodeRuntimeProviderRegistry"/> 가 lookup 실패 시 이 인스턴스 반환.
	/// </summary>
	public sealed class DefaultNodeRuntimeViewProvider : INodeRuntimeViewProvider
	{
		public VisualElement Build(NodeBase node) => null;
	}
}
