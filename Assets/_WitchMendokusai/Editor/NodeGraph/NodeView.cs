using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

// `WitchMendokusai.Node` (BT 행동트리) 가 enclosing namespace 에 있어 GraphView Node 와 충돌 — alias 로 명시.
using GraphViewNode = UnityEditor.Experimental.GraphView.Node;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// `NodeBase` 의 GraphView 시각 표현 — GraphView `Node` 상속. 포트는 `NodePort` ↔ `Port` 1:1 매핑.
	/// `Owner.Id` + `PortId` 가 모델 키, Port 인스턴스가 view 키. 양방향 lookup 지원.
	/// </summary>
	public class NodeView : GraphViewNode
	{
		public NodeBase Node { get; }

		private readonly Dictionary<string, Port> inputPortsByPortId = new();
		private readonly Dictionary<string, Port> outputPortsByPortId = new();
		private readonly Dictionary<Port, NodePort> portViewToModel = new();

		public NodeView(NodeBase node)
		{
			Node = node;
			title = NicifyTypeName(node.GetType().Name);

			SetPosition(new Rect(node.EditorPosition, new Vector2(200, 100)));

			foreach (NodePort port in node.Ports)
			{
				Direction direction = port.Direction == PortDirection.Input ? Direction.Input : Direction.Output;
				Port.Capacity capacity = port.Direction == PortDirection.Input ? Port.Capacity.Single : Port.Capacity.Multi;
				Port portView = InstantiatePort(Orientation.Horizontal, direction, capacity, port.DataType);
				portView.portName = port.PortId;
				portView.portColor = ColorForType(port.DataType);

				if (port.Direction == PortDirection.Input)
				{
					inputContainer.Add(portView);
					inputPortsByPortId[port.PortId] = portView;
				}
				else
				{
					outputContainer.Add(portView);
					outputPortsByPortId[port.PortId] = portView;
				}
				portViewToModel[portView] = port;
			}

			RefreshExpandedState();
			RefreshPorts();
		}

		public Port GetInputPortView(string portId) => inputPortsByPortId.TryGetValue(portId, out Port p) ? p : null;
		public Port GetOutputPortView(string portId) => outputPortsByPortId.TryGetValue(portId, out Port p) ? p : null;
		public NodePort GetNodePortByView(Port view) => portViewToModel.TryGetValue(view, out NodePort np) ? np : null;

		private static string NicifyTypeName(string typeName)
		{
			if (typeName.EndsWith("Node"))
				typeName = typeName[..^4];
			return typeName;
		}

		/// <summary>타입별 포트 색 — 같은 타입끼리만 연결 가능한 시각 단서.</summary>
		private static Color ColorForType(System.Type type)
		{
			if (type == typeof(float)) return new Color(0.9f, 0.9f, 0.4f);
			if (type == typeof(int)) return new Color(0.4f, 0.8f, 0.9f);
			if (type == typeof(bool)) return new Color(0.9f, 0.4f, 0.4f);
			if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)) return new Color(0.4f, 0.9f, 0.6f);
			if (type == typeof(Color)) return new Color(0.9f, 0.6f, 0.4f);
			return new Color(0.7f, 0.7f, 0.7f);
		}
	}
}
