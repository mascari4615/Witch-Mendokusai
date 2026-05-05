using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// `WitchMendokusai.Node` (BT 행동트리) 가 enclosing namespace 에 있어 GraphView Node 와 충돌 — alias 로 명시.
using GraphViewNode = UnityEditor.Experimental.GraphView.Node;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// `NodeBase` 의 GraphView 시각 표현 — GraphView `Node` 상속. 포트는 `NodePort` ↔ `Port` 1:1 매핑.
	/// `Owner.Id` + `PortId` 가 모델 키, Port 인스턴스가 view 키. 양방향 lookup 지원.
	/// `graph` 가 있고 노드에 float 출력 포트 있으면 64×64 grayscale 미리보기 (extensionContainer).
	/// </summary>
	public class NodeView : GraphViewNode
	{
		public NodeBase Node { get; }

		private readonly Dictionary<string, Port> inputPortsByPortId = new();
		private readonly Dictionary<string, Port> outputPortsByPortId = new();
		private readonly Dictionary<Port, NodePort> portViewToModel = new();

		public NodeView(NodeBase node, NodeGraph graph)
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

			// float-output 노드면 64×64 grayscale 미리보기 — extension 영역 (노드 expand 시 보임)
			NodePort floatOutput = node.OutputPorts.FirstOrDefault(p => p.DataType == typeof(float));
			if (floatOutput != null && graph != null)
			{
				Texture2D preview = NodePreview.RenderFloatOutputPreview(node, graph);
				if (preview != null)
				{
					VisualElement previewElement = new()
					{
						style =
						{
							width = NodePreview.PREVIEW_SIZE,
							height = NodePreview.PREVIEW_SIZE,
							marginTop = 4,
							marginBottom = 4,
							alignSelf = Align.Center,
							backgroundImage = new StyleBackground(preview),
						}
					};
					extensionContainer.Add(previewElement);
				}
			}

			// SerializeField 인스펙터 embed — ShaderGraph 식. 노드 펼침 시 슬라이더 직접 편집.
			// `[SerializeReference]` 안 노드의 SerializeField 들을 PropertyField 가 자동 펼쳐줌.
			// parameter hash 변경 → 영역 캐시 invalidate (노드 자체 책임) → preview 재 sim.
			if (graph != null)
			{
				int nodeIndex = -1;
				for (int i = 0; i < graph.Nodes.Count; i++)
				{
					if (ReferenceEquals(graph.Nodes[i], node))
					{
						nodeIndex = i;
						break;
					}
				}
				if (nodeIndex >= 0)
				{
					SerializedObject serializedGraph = new(graph);
					SerializedProperty nodesProp = serializedGraph.FindProperty("nodes");
					if (nodesProp != null && nodeIndex < nodesProp.arraySize)
					{
						SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(nodeIndex);
						PropertyField propertyField = new(nodeProp)
						{
							style =
							{
								marginTop = 4,
								marginBottom = 4,
								marginLeft = 4,
								marginRight = 4,
								minWidth = 220,
							},
						};
						propertyField.Bind(serializedGraph);
						extensionContainer.Add(propertyField);
					}
				}
			}

			RefreshExpandedState();
			RefreshPorts();
			expanded = true; // extension (preview + 인스펙터) 기본 노출
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
