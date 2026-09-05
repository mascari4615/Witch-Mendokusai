using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// TASK-WM-034 단계 A 검증 — `Constant(3) + Constant(5) → Add → Output` 그래프를
	/// 프로그래매틱 빌드 + Pull 실행 → 콘솔 `[OutputFloatNode] 8` 확인.
	/// </summary>
	public static class NodeGraphTestMenu
	{
		private const string TEST_GRAPH_FOLDER = "Assets/_WitchMendokusai/Core/NodeGraph/Test";
		private const string TEST_GRAPH_PATH = TEST_GRAPH_FOLDER + "/TestGraph.asset";

		[MenuItem("WM/NodeGraph/Build Test Graph")]
		public static void BuildTestGraph()
		{
			EnsureFolder(TEST_GRAPH_FOLDER);

			NodeGraph graph = AssetDatabase.LoadAssetAtPath<NodeGraph>(TEST_GRAPH_PATH);
			if (graph == null)
			{
				graph = ScriptableObject.CreateInstance<NodeGraph>();
				AssetDatabase.CreateAsset(graph, TEST_GRAPH_PATH);
			}

			graph.Clear();

			ConstantFloatNode c3 = new() { Value = 3f };
			c3.EditorPosition = new Vector2(0f, 0f);
			ConstantFloatNode c5 = new() { Value = 5f };
			c5.EditorPosition = new Vector2(0f, 120f);
			AddFloatNode add = new();
			add.EditorPosition = new Vector2(220f, 60f);
			OutputFloatNode output = new();
			output.EditorPosition = new Vector2(440f, 60f);

			graph.AddNode(c3);
			graph.AddNode(c5);
			graph.AddNode(add);
			graph.AddNode(output);

			bool ok = true;
			ok &= graph.Connect(c3.FindPort("out"), add.FindPort("a"));
			ok &= graph.Connect(c5.FindPort("out"), add.FindPort("b"));
			ok &= graph.Connect(add.FindPort("result"), output.FindPort("in"));

			if (ok == false)
				Debug.LogError("[NodeGraphTestMenu] 연결 실패 — port 타입/방향 검증 실패.");

			EditorUtility.SetDirty(graph);
			AssetDatabase.SaveAssets();
			Debug.Log($"[NodeGraphTestMenu] TestGraph built — {graph.Nodes.Count} 노드, {graph.Connections.Count} 연결. Run Test Graph 메뉴로 실행.");
		}

		[MenuItem("WM/NodeGraph/Run Test Graph")]
		public static void RunTestGraph()
		{
			NodeGraph graph = AssetDatabase.LoadAssetAtPath<NodeGraph>(TEST_GRAPH_PATH);
			if (graph == null)
			{
				Debug.LogError($"[NodeGraphTestMenu] {TEST_GRAPH_PATH} 없음. 먼저 Build Test Graph 실행.");
				return;
			}

			NodeExecutionContext context = new(graph);
			int terminalCount = 0;
			foreach (NodeBase node in graph.Nodes)
			{
				if (node is OutputFloatNode)
				{
					context.Evaluate(node);
					terminalCount++;
				}
			}
			Debug.Log($"[NodeGraphTestMenu] 그래프 실행 완료 — terminal {terminalCount}개 평가.");
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;
			string parent = Path.GetDirectoryName(path).Replace("\\", "/");
			string folderName = Path.GetFileName(path);
			if (AssetDatabase.IsValidFolder(parent) == false)
				EnsureFolder(parent);
			AssetDatabase.CreateFolder(parent, folderName);
		}
	}
}
