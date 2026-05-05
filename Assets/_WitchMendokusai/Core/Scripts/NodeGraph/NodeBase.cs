using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 노드 베이스. ScriptableObject 가 아니라 plain class — `NodeGraph` SO 안에서 `[SerializeReference]` 으로
	/// polymorphic 직렬화. 도메인별 구체 노드는 이를 상속 (예: `ConstantFloatNode`, `PerlinNode`).
	///
	/// 포트는 직렬화하지 않음 — 매 인스턴스 lifetime 의 첫 <see cref="Ports"/> 접근 시 <see cref="CreatePorts"/> 로
	/// 구성. 안정 식별은 `Id` (노드) + `PortId` (포트 — 서브클래스가 hard-code).
	///
	/// `Evaluate` 는 template method — 베이스가 포트 초기화 보장 후 <see cref="OnEvaluate"/> 호출.
	/// </summary>
	[Serializable]
	public abstract class NodeBase
	{
		[SerializeField] private string id;
		[SerializeField] private Vector2 editorPosition;

		[NonSerialized] private List<NodePort> ports;

		public string Id => id;
		public Vector2 EditorPosition { get => editorPosition; set => editorPosition = value; }

		public IReadOnlyList<NodePort> Ports
		{
			get
			{
				if (ports == null)
					ports = new List<NodePort>(CreatePorts());
				return ports;
			}
		}

		public IEnumerable<NodePort> InputPorts => Ports.Where(p => p.Direction == PortDirection.Input);
		public IEnumerable<NodePort> OutputPorts => Ports.Where(p => p.Direction == PortDirection.Output);

		/// <summary>
		/// 신규 노드 생성 시 (parameterless ctor) — 고유 GUID 발급.
		/// `[SerializeReference]` 역직렬화도 ctor 호출하지만, field 값은 deserialize 가 덮어씀 — 기존 id 보존.
		/// </summary>
		protected NodeBase()
		{
			id = Guid.NewGuid().ToString("N");
		}

		/// <summary>이 노드가 가진 포트 정의. 서브클래스 override. 베이스가 1회만 호출 + 캐시.</summary>
		protected abstract IEnumerable<NodePort> CreatePorts();

		/// <summary>Pull 실행기가 호출. 포트 초기화 보장 후 <see cref="OnEvaluate"/> 위임.</summary>
		public void Evaluate(NodeExecutionContext context)
		{
			_ = Ports; // 포트 lazy 생성 강제 — 서브클래스 OnEvaluate 안에서 port 필드 사용 안전
			OnEvaluate(context);
		}

		/// <summary>
		/// 서브클래스 구현 — `context.GetInput&lt;T&gt;(inputPort)` 으로 의존 평가, `context.SetOutput&lt;T&gt;(outputPort, value)` 로 결과.
		/// </summary>
		protected abstract void OnEvaluate(NodeExecutionContext context);

		public NodePort FindPort(string portId)
		{
			foreach (NodePort p in Ports)
				if (p.PortId == portId)
					return p;
			return null;
		}
	}
}
