using System.IO;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 그래프 부트스트랩 — `Build Default Terrain Graph` 메뉴가 기본 Perlin 그래프를 자동 빌드 +
	/// active TerrainParameters 에 자동 assign. 사용자가 그래프 시스템 즉시 검증 가능.
	/// </summary>
	public static class TerrainGraphMenu
	{
		private const string TERRAIN_FOLDER = "Assets/_WitchMendokusai/Core/Scripts/Terrain";
		private const string GRAPH_PATH = TERRAIN_FOLDER + "/DefaultTerrainGraph.asset";

		[MenuItem("WitchMendokusai/Terrain/Build Default Terrain Graph")]
		public static void BuildDefaultTerrainGraph()
		{
			TerrainGraph graph = AssetDatabase.LoadAssetAtPath<TerrainGraph>(GRAPH_PATH);
			if (graph == null)
			{
				graph = ScriptableObject.CreateInstance<TerrainGraph>();
				AssetDatabase.CreateAsset(graph, GRAPH_PATH);
			}

			graph.Clear();

			WorldPositionInputNode posNode = new();
			posNode.EditorPosition = new Vector2(0f, 0f);

			FractalPerlinNode perlinNode = new()
			{
				Frequency = 0.01f,
				Amplitude = 32f,
				Octaves = 4,
				Persistence = 0.5f,
				Lacunarity = 2f,
				Seed = 0,
			};
			perlinNode.EditorPosition = new Vector2(260f, 0f);

			HydraulicErosionNode hydraulicNode = new();
			hydraulicNode.EditorPosition = new Vector2(540f, 0f);

			ThermalErosionNode thermalNode = new();
			thermalNode.EditorPosition = new Vector2(820f, 0f);

			HeightOutputNode outputNode = new();
			outputNode.EditorPosition = new Vector2(1100f, 0f);

			graph.AddNode(posNode);
			graph.AddNode(perlinNode);
			graph.AddNode(hydraulicNode);
			graph.AddNode(thermalNode);
			graph.AddNode(outputNode);

			bool ok = true;
			ok &= graph.Connect(posNode.FindPort("x"), perlinNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), perlinNode.FindPort("z"));
			ok &= graph.Connect(posNode.FindPort("x"), hydraulicNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), hydraulicNode.FindPort("z"));
			ok &= graph.Connect(posNode.FindPort("x"), thermalNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), thermalNode.FindPort("z"));
			// erosion 노드들은 "height" 가 input/output 둘 다 — direction 명시 필수.
			ok &= graph.Connect(perlinNode.FindPort("height"), hydraulicNode.FindPort("height", PortDirection.Input));
			ok &= graph.Connect(hydraulicNode.FindPort("height", PortDirection.Output), thermalNode.FindPort("height", PortDirection.Input));
			ok &= graph.Connect(thermalNode.FindPort("height", PortDirection.Output), outputNode.FindPort("height"));

			if (ok == false)
				Debug.LogError("[TerrainGraphMenu] 연결 실패 — port 타입/방향 검증 실패.");

			EditorUtility.SetDirty(graph);

			TerrainParameters active = TerrainParametersService.Active;
			if (active != null)
			{
				SerializedObject serialized = new(active);
				SerializedProperty graphProp = serialized.FindProperty("terrainGraph");
				if (graphProp != null)
				{
					graphProp.objectReferenceValue = graph;
					serialized.ApplyModifiedProperties();
					EditorUtility.SetDirty(active);
					Debug.Log($"[TerrainGraphMenu] DefaultTerrainGraph 자동 assign → {active.name} (TerrainParameters Active).");
				}
			}
			else
			{
				Debug.LogWarning("[TerrainGraphMenu] Active TerrainParameters 없음 — 그래프 생성만 함, 수동 assign 필요.");
			}

			AssetDatabase.SaveAssets();
			Debug.Log($"[TerrainGraphMenu] DefaultTerrainGraph 빌드 완료 — {graph.Nodes.Count} 노드, {graph.Connections.Count} 연결. Path: {GRAPH_PATH}");
		}
	}
}
