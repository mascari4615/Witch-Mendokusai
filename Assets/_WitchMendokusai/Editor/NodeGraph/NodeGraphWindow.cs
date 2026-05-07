using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// `NodeGraph` SO 편집 EditorWindow. asset 더블클릭 또는 메뉴로 open.
	/// `NodeGraphView` (GraphView) 호스트. graph SO 변경은 view 가 자동 sync (graphViewChanged → SetDirty).
	/// </summary>
	public class NodeGraphWindow : EditorWindow
	{
		[SerializeField] private NodeGraph graph;

		private NodeGraphView graphView;
		private Label headerLabel;

		[MenuItem("WM/NodeGraph/Open Active Graph (Test)")]
		public static void OpenActive()
		{
			NodeGraph asset = AssetDatabase.LoadAssetAtPath<NodeGraph>("Assets/_WitchMendokusai/Core/Scripts/NodeGraph/Test/TestGraph.asset");
			if (asset == null)
			{
				Debug.LogError("[NodeGraphWindow] TestGraph 자산 없음. Build Test Graph 메뉴 먼저.");
				return;
			}
			Open(asset);
		}

		[OnOpenAsset]
		public static bool OnOpenAsset(int instanceID, int line)
		{
			Object obj = EditorUtility.InstanceIDToObject(instanceID);
			if (obj is NodeGraph nodeGraph)
			{
				Open(nodeGraph);
				return true;
			}
			return false;
		}

		public static void Open(NodeGraph graph)
		{
			NodeGraphWindow window = GetWindow<NodeGraphWindow>();
			window.titleContent = new GUIContent($"NodeGraph: {graph.name}");
			window.minSize = new Vector2(600, 400);
			window.LoadGraph(graph);
			window.Show();
		}

		private void OnEnable()
		{
			BuildLayout();
			if (graph != null)
				LoadGraph(graph);
		}

		private void BuildLayout()
		{
			rootVisualElement.Clear();

			headerLabel = new Label("그래프 미선택")
			{
				style =
				{
					unityFontStyleAndWeight = FontStyle.Bold,
					marginLeft = 8,
					marginTop = 4,
					marginBottom = 4,
				}
			};
			rootVisualElement.Add(headerLabel);
		}

		private void LoadGraph(NodeGraph g)
		{
			graph = g;

			if (graphView != null)
			{
				rootVisualElement.Remove(graphView);
				graphView = null;
			}

			if (graph == null)
			{
				headerLabel.text = "그래프 미선택";
				return;
			}

			headerLabel.text = $"그래프: {graph.name}    |    노드 {graph.Nodes.Count}    연결 {graph.Connections.Count}";

			graphView = new NodeGraphView(graph);
			graphView.style.flexGrow = 1;
			rootVisualElement.Add(graphView);
		}
	}
}
