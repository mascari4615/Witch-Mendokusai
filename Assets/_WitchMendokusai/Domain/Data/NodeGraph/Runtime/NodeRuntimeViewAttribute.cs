using System;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// Provider → 노드 타입 매핑 attribute. <see cref="NodeRuntimeProviderRegistry"/> 가 reflection 으로 수집.
	///
	/// 사용 예:
	/// <code>
	/// [NodeRuntimeView(typeof(QuestNode))]
	/// public sealed class QuestNodeRuntimeViewProvider : INodeRuntimeViewProvider { ... }
	/// </code>
	///
	/// 한 노드 타입에 여러 Provider 등록 시 마지막 발견본이 승리 (Dictionary overwrite). 도메인 충돌은 컨벤션으로 방지.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class NodeRuntimeViewAttribute : Attribute
	{
		public Type NodeType { get; }

		public NodeRuntimeViewAttribute(Type nodeType)
		{
			NodeType = nodeType;
		}
	}
}
