using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 런타임 노드 그래프 시각 (UI Toolkit). NodeGraph SO 받아 노드/엣지 인스턴스화 + 화면 표시.
	/// Editor 측 GraphView (단계 B) 와 sibling — 데이터 모델 (NodeGraph + NodeBase + NodeConnection) 공유.
	///
	/// H1 단계 (2026-05-08): generic only — 라벨+박스 노드 + Painter2D 라인 엣지. read-only.
	/// 후속 단계: H2 줌/팬, H3 Provider 패턴 (도메인별 커스텀 비주얼), H4 클릭/hover 이벤트.
	/// </summary>
	public class NodeGraphRuntimeView : VisualElement
	{
		public const string USS_CLASS = "wm-nodegraph-runtime";
		public const string USS_CANVAS = "wm-nodegraph-runtime__canvas";

		private readonly VisualElement canvas;
		private readonly Dictionary<string, NodeRuntimeElement> nodeElementsById = new();
		private readonly List<EdgeRuntimeElement> edgeElements = new();

		private NodeGraph boundGraph;

		public IReadOnlyDictionary<string, NodeRuntimeElement> NodeElementsById => nodeElementsById;

		public NodeGraphRuntimeView()
		{
			AddToClassList(USS_CLASS);

			style.overflow = Overflow.Hidden;
			style.flexGrow = 1;

			canvas = new VisualElement();
			canvas.AddToClassList(USS_CANVAS);
			canvas.style.position = Position.Absolute;
			canvas.style.left = 0;
			canvas.style.top = 0;
			canvas.style.right = 0;
			canvas.style.bottom = 0;
			canvas.pickingMode = PickingMode.Ignore;
			Add(canvas);
		}

		/// <summary>그래프 바인딩 — 호출마다 전체 재구성. 노드/엣지 위치는 NodeBase.EditorPosition + NodeConnection.</summary>
		public void Bind(NodeGraph graph)
		{
			boundGraph = graph;
			Refresh();
		}

		/// <summary>그래프 데이터 변동 시 명시 호출. (자동 hot reload 는 1차 X — 후속 H 후속.)</summary>
		public void Refresh()
		{
			canvas.Clear();
			nodeElementsById.Clear();
			edgeElements.Clear();

			if (boundGraph == null)
				return;

			foreach (NodeBase node in boundGraph.Nodes)
			{
				if (node == null)
					continue;

				NodeRuntimeElement nodeElement = new();
				nodeElement.Bind(node);
				nodeElement.style.position = Position.Absolute;
				nodeElement.style.left = node.EditorPosition.x;
				nodeElement.style.top = node.EditorPosition.y;
				nodeElementsById[node.Id] = nodeElement;
			}

			foreach (NodeConnection connection in boundGraph.Connections)
			{
				if (connection == null)
					continue;

				if (nodeElementsById.TryGetValue(connection.SourceNodeId, out NodeRuntimeElement sourceElement) == false)
					continue;

				if (nodeElementsById.TryGetValue(connection.TargetNodeId, out NodeRuntimeElement targetElement) == false)
					continue;

				EdgeRuntimeElement edgeElement = new();
				edgeElement.SetEndpoints(sourceElement, targetElement);
				canvas.Add(edgeElement);
				edgeElements.Add(edgeElement);
			}

			foreach (NodeRuntimeElement nodeElement in nodeElementsById.Values)
				canvas.Add(nodeElement);
		}
	}
}
