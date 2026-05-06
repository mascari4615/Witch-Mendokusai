using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// `NodeGraph` SO 의 GraphView 시각 — `GraphView` 상속. 노드/연결 시각 + 사용자 편집 → SO 양방향 sync.
	/// 노드 이동 / 연결 / 삭제 / 신규 (우클릭 메뉴) 모두 graphViewChanged 또는 menu 콜백 → graph 함수 호출.
	/// </summary>
	public class NodeGraphView : GraphView
	{
		private readonly NodeGraph graph;
		private readonly Dictionary<string, NodeView> nodeViews = new();

		public NodeGraphView(NodeGraph graph)
		{
			this.graph = graph;

			this.AddManipulator(new ContentZoomer());
			this.AddManipulator(new ContentDragger());
			this.AddManipulator(new SelectionDragger());
			this.AddManipulator(new RectangleSelector());

			GridBackground grid = new();
			Insert(0, grid);
			grid.StretchToParentSize();

			style.flexGrow = 1;

			Refresh();

			graphViewChanged = OnGraphViewChanged;
		}

		public void Refresh()
		{
			// DeleteElements 는 graphViewChanged callback 을 fire — 그 callback 이 graph 에서 실제 노드 삭제.
			// Refresh 는 *시각만* 재구성해야 하므로 callback 일시 무효화.
			GraphViewChanged previous = graphViewChanged;
			graphViewChanged = null;
			try
			{
				DeleteElements(graphElements.ToList());
				nodeViews.Clear();

				foreach (NodeBase node in graph.Nodes)
				{
					NodeView view = new(node, graph);
					AddElement(view);
					nodeViews[node.Id] = view;
				}

				foreach (NodeConnection conn in graph.Connections)
				{
					if (nodeViews.TryGetValue(conn.SourceNodeId, out NodeView sourceView) == false)
						continue;
					if (nodeViews.TryGetValue(conn.TargetNodeId, out NodeView targetView) == false)
						continue;

					Port outPort = sourceView.GetOutputPortView(conn.SourcePortId);
					Port inPort = targetView.GetInputPortView(conn.TargetPortId);
					if (outPort == null || inPort == null)
						continue;

					Edge edge = outPort.ConnectTo(inPort);
					AddElement(edge);
				}
			}
			finally
			{
				graphViewChanged = previous;
			}
		}

		private GraphViewChange OnGraphViewChanged(GraphViewChange change)
		{
			bool dirty = false;

			if (change.elementsToRemove != null)
			{
				foreach (GraphElement elt in change.elementsToRemove)
				{
					if (elt is Edge edge)
					{
						NodeConnection conn = FindConnectionForEdge(edge);
						if (conn != null)
						{
							graph.Disconnect(conn);
							dirty = true;
						}
					}
					else if (elt is NodeView nv)
					{
						graph.RemoveNode(nv.Node);
						nodeViews.Remove(nv.Node.Id);
						dirty = true;
					}
				}
			}

			if (change.edgesToCreate != null)
			{
				foreach (Edge edge in change.edgesToCreate)
				{
					NodeView sourceView = edge.output.node as NodeView;
					NodeView targetView = edge.input.node as NodeView;
					if (sourceView == null || targetView == null)
						continue;

					NodePort sourcePort = sourceView.GetNodePortByView(edge.output);
					NodePort targetPort = targetView.GetNodePortByView(edge.input);
					if (sourcePort == null || targetPort == null)
						continue;

					if (graph.Connect(sourcePort, targetPort))
						dirty = true;
				}
			}

			if (change.movedElements != null)
			{
				foreach (GraphElement elt in change.movedElements)
				{
					if (elt is NodeView nv)
					{
						Vector2 pos = nv.GetPosition().position;
						nv.Node.EditorPosition = pos;
						dirty = true;
					}
				}
			}

			if (dirty)
				EditorUtility.SetDirty(graph);

			return change;
		}

		private NodeConnection FindConnectionForEdge(Edge edge)
		{
			NodeView sourceView = edge.output?.node as NodeView;
			NodeView targetView = edge.input?.node as NodeView;
			if (sourceView == null || targetView == null)
				return null;
			NodePort sourcePort = sourceView.GetNodePortByView(edge.output);
			NodePort targetPort = targetView.GetNodePortByView(edge.input);
			if (sourcePort == null || targetPort == null)
				return null;
			foreach (NodeConnection c in graph.Connections)
			{
				if (c.SourceNodeId == sourcePort.Owner.Id && c.SourcePortId == sourcePort.PortId
					&& c.TargetNodeId == targetPort.Owner.Id && c.TargetPortId == targetPort.PortId)
					return c;
			}
			return null;
		}

		/// <summary>드래그 시 호환 포트만 highlight — 같은 타입 + 반대 방향 + 다른 노드.</summary>
		public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
		{
			return ports.ToList().Where(p =>
				p.direction != startPort.direction &&
				p.node != startPort.node &&
				p.portType == startPort.portType
			).ToList();
		}

		public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
		{
			Vector2 mousePos = evt.localMousePosition;

			foreach (Type nodeType in NodeRegistry.AllNodeTypes())
			{
				Type capturedType = nodeType;
				evt.menu.AppendAction($"Create/{NicifyTypeName(nodeType.Name)}", action =>
				{
					Vector2 worldPos = action.eventInfo.mousePosition;
					Vector2 graphLocal = contentViewContainer.WorldToLocal(worldPos);
					CreateNodeAt(capturedType, graphLocal);
				});
			}

			base.BuildContextualMenu(evt);
		}

		private void CreateNodeAt(Type type, Vector2 pos)
		{
			NodeBase node = (NodeBase)Activator.CreateInstance(type);
			node.EditorPosition = pos;
			graph.AddNode(node);
			EditorUtility.SetDirty(graph);
			Refresh();
		}

		/// <summary>
		/// 연결 방향 기반 위상 정렬 후 겹침 없이 재배치. 열 간격 420px, 행 간격 260px.
		/// 이동 후 SO dirty 마킹 + FrameAll 로 전체 보이게 줌.
		/// </summary>
		public void AutoLayout()
		{
			if (graph.Nodes.Count == 0)
				return;

			// 각 노드의 in-degree (incoming 연결 수) 계산
			Dictionary<string, int> inDegree = new();
			Dictionary<string, List<string>> successors = new();

			foreach (NodeBase node in graph.Nodes)
			{
				inDegree[node.Id] = 0;
				successors[node.Id] = new List<string>();
			}

			foreach (NodeConnection conn in graph.Connections)
			{
				inDegree[conn.TargetNodeId]++;
				successors[conn.SourceNodeId].Add(conn.TargetNodeId);
			}

			// Kahn's BFS — 위상 정렬하며 depth 할당
			Dictionary<string, int> depth = new();
			Queue<string> queue = new();

			foreach (NodeBase node in graph.Nodes)
			{
				if (inDegree[node.Id] == 0)
				{
					queue.Enqueue(node.Id);
					depth[node.Id] = 0;
				}
			}

			while (queue.Count > 0)
			{
				string current = queue.Dequeue();
				foreach (string successor in successors[current])
				{
					if (depth.ContainsKey(successor) == false)
						depth[successor] = 0;
					depth[successor] = Mathf.Max(depth[successor], depth[current] + 1);
					inDegree[successor]--;
					if (inDegree[successor] == 0)
						queue.Enqueue(successor);
				}
			}

			// 사이클 있는 노드 — depth 미할당. 마지막 열에 배치.
			int maxDepth = depth.Count > 0 ? depth.Values.Max() : 0;
			foreach (NodeBase node in graph.Nodes)
			{
				if (depth.ContainsKey(node.Id) == false)
					depth[node.Id] = maxDepth + 1;
			}

			// 같은 depth 내 행 인덱스 할당
			Dictionary<int, List<string>> columns = new();
			foreach (KeyValuePair<string, int> kv in depth)
			{
				if (columns.ContainsKey(kv.Value) == false)
					columns[kv.Value] = new List<string>();
				columns[kv.Value].Add(kv.Key);
			}

			const float COLUMN_SPACING = 420f;
			const float ROW_SPACING = 260f;

			Dictionary<string, NodeBase> nodeById = graph.Nodes.ToDictionary(n => n.Id);

			foreach (KeyValuePair<int, List<string>> col in columns)
			{
				float x = col.Key * COLUMN_SPACING;
				float totalHeight = (col.Value.Count - 1) * ROW_SPACING;
				float startY = -totalHeight * 0.5f;

				for (int i = 0; i < col.Value.Count; i++)
				{
					string nodeId = col.Value[i];
					if (nodeById.TryGetValue(nodeId, out NodeBase node) == false)
						continue;

					Vector2 newPos = new(x, startY + i * ROW_SPACING);
					node.EditorPosition = newPos;

					if (nodeViews.TryGetValue(nodeId, out NodeView view))
						view.SetPosition(new Rect(newPos, view.GetPosition().size));
				}
			}

			EditorUtility.SetDirty(graph);
			FrameAll();
		}

		private static string NicifyTypeName(string typeName)
		{
			if (typeName.EndsWith("Node"))
				typeName = typeName[..^4];
			return typeName;
		}
	}
}
