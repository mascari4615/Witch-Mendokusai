using System;

namespace WitchMendokusai
{
	public enum PortDirection
	{
		Input,
		Output
	}

	/// <summary>
	/// Non-generic 베이스 — 노드의 List&lt;NodePort&gt; 가 다른 타입 포트를 같이 담을 수 있게.
	/// 실제 데이터 타입은 <see cref="DataType"/> + 서브클래스 <see cref="NodePort{T}"/>.
	/// 직렬화 X — `NodeBase` 가 매 lazy init 시 `CreatePorts` 로 재구성 (포트 인스턴스 자체는 일시적, 안정 식별은 `Owner.Id` + `PortId`).
	/// </summary>
	public abstract class NodePort
	{
		public NodeBase Owner { get; }
		public string PortId { get; }
		public PortDirection Direction { get; }
		public abstract Type DataType { get; }

		protected NodePort(NodeBase owner, string portId, PortDirection direction)
		{
			Owner = owner;
			PortId = portId;
			Direction = direction;
		}
	}

	/// <summary>
	/// 타입 안전 포트 — `NodeExecutionContext.GetInput&lt;T&gt;` / `SetOutput&lt;T&gt;` 호출 시 컴파일러가 T 일치 검증.
	/// 그래프 연결 검증 (`NodeGraph.Connect`) 도 `DataType` 비교로 차단.
	/// </summary>
	public sealed class NodePort<T> : NodePort
	{
		public override Type DataType => typeof(T);

		public NodePort(NodeBase owner, string portId, PortDirection direction)
			: base(owner, portId, direction)
		{
		}
	}
}
