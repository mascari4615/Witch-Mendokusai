using System.IO;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 그래프 부트스트랩 메뉴 모음.
	/// - Build Default Terrain Graph: 기본 linear chain (7-노드) 자동 빌드 + active TerrainParameters assign.
	/// - Build Threshold+Lerp Demo Graph: H3 DAG 데모 — 두 Perlin + Threshold + Lerp.
	/// </summary>
	public static class TerrainGraphMenu
	{
		private const string TERRAIN_FOLDER = "Assets/_WitchMendokusai/Core/Scripts/Terrain";
		private const string GRAPH_PATH = TERRAIN_FOLDER + "/DefaultTerrainGraph.asset";
		private const string DEMO_GRAPH_PATH = TERRAIN_FOLDER + "/ThresholdLerpDemoGraph.asset";

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
			perlinNode.EditorPosition = new Vector2(420f, 0f);

			HydraulicErosionNode hydraulicNode = new();
			hydraulicNode.EditorPosition = new Vector2(840f, 0f);

			ThermalErosionNode thermalNode = new();
			thermalNode.EditorPosition = new Vector2(1260f, 0f);

			SmoothFilterNode smoothNode = new();
			smoothNode.EditorPosition = new Vector2(1680f, 0f);

			CurveFilterNode curveNode = new();
			curveNode.EditorPosition = new Vector2(2100f, 0f);

			HeightOutputNode outputNode = new();
			outputNode.EditorPosition = new Vector2(2520f, 0f);

			graph.AddNode(posNode);
			graph.AddNode(perlinNode);
			graph.AddNode(hydraulicNode);
			graph.AddNode(thermalNode);
			graph.AddNode(smoothNode);
			graph.AddNode(curveNode);
			graph.AddNode(outputNode);

			bool ok = true;
			ok &= graph.Connect(posNode.FindPort("x"), perlinNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), perlinNode.FindPort("z"));
			ok &= graph.Connect(posNode.FindPort("x"), hydraulicNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), hydraulicNode.FindPort("z"));
			ok &= graph.Connect(posNode.FindPort("x"), thermalNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), thermalNode.FindPort("z"));
			ok &= graph.Connect(posNode.FindPort("x"), smoothNode.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), smoothNode.FindPort("z"));
			// region grid 노드들은 "height" 가 input/output 둘 다 — direction 명시 필수.
			// CurveFilterNode (PointFilterNodeBase) 는 inX/inZ 없음 — Pull chain 이 GlobalInput 좌표로 source 자동 평가.
			ok &= graph.Connect(perlinNode.FindPort("height"), hydraulicNode.FindPort("height", PortDirection.Input));
			ok &= graph.Connect(hydraulicNode.FindPort("height", PortDirection.Output), thermalNode.FindPort("height", PortDirection.Input));
			ok &= graph.Connect(thermalNode.FindPort("height", PortDirection.Output), smoothNode.FindPort("height", PortDirection.Input));
			ok &= graph.Connect(smoothNode.FindPort("height", PortDirection.Output), curveNode.FindPort("height", PortDirection.Input));
			ok &= graph.Connect(curveNode.FindPort("height", PortDirection.Output), outputNode.FindPort("height"));

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

		/// <summary>
		/// H3 DAG 데모 — WorldPositionInput → Perlin_A + Perlin_B → Threshold → Lerp(A, B, t) → Output.
		/// DefaultTerrainGraph 의 linear chain 정합 유지를 위해 별도 asset.
		/// </summary>
		[MenuItem("WitchMendokusai/Terrain/Build Threshold+Lerp Demo Graph")]
		public static void BuildThresholdLerpDemoGraph()
		{
			TerrainGraph graph = AssetDatabase.LoadAssetAtPath<TerrainGraph>(DEMO_GRAPH_PATH);
			if (graph == null)
			{
				graph = ScriptableObject.CreateInstance<TerrainGraph>();
				AssetDatabase.CreateAsset(graph, DEMO_GRAPH_PATH);
			}

			graph.Clear();

			WorldPositionInputNode posNode = new();
			posNode.EditorPosition = new Vector2(0f, 0f);

			// Perlin A — 저지대 (낮은 주파수, 완만)
			FractalPerlinNode perlinA = new()
			{
				Frequency = 0.008f,
				Amplitude = 20f,
				Octaves = 3,
				Persistence = 0.5f,
				Lacunarity = 2f,
				Seed = 0,
			};
			perlinA.EditorPosition = new Vector2(420f, -150f);

			// Perlin B — 고지대 (높은 주파수, 험준)
			FractalPerlinNode perlinB = new()
			{
				Frequency = 0.015f,
				Amplitude = 50f,
				Octaves = 5,
				Persistence = 0.6f,
				Lacunarity = 2f,
				Seed = 42,
			};
			perlinB.EditorPosition = new Vector2(420f, 150f);

			// Threshold — 25m 기준, 5m blend 폭
			ThresholdFilterNode thresholdNode = new();
			thresholdNode.EditorPosition = new Vector2(840f, 0f);

			// Lerp — Perlin_A(저지대) ↔ Perlin_B(고지대) blend
			LerpNode lerpNode = new();
			lerpNode.EditorPosition = new Vector2(1260f, 0f);

			HeightOutputNode outputNode = new();
			outputNode.EditorPosition = new Vector2(1680f, 0f);

			graph.AddNode(posNode);
			graph.AddNode(perlinA);
			graph.AddNode(perlinB);
			graph.AddNode(thresholdNode);
			graph.AddNode(lerpNode);
			graph.AddNode(outputNode);

			bool ok = true;
			ok &= graph.Connect(posNode.FindPort("x"), perlinA.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), perlinA.FindPort("z"));
			ok &= graph.Connect(posNode.FindPort("x"), perlinB.FindPort("x"));
			ok &= graph.Connect(posNode.FindPort("z"), perlinB.FindPort("z"));
			// Threshold 는 PointFilterNodeBase — inX/inZ 없음. height 만.
			ok &= graph.Connect(perlinA.FindPort("height"), thresholdNode.FindPort("height", PortDirection.Input));
			// Lerp: a=저지대, b=고지대, t=threshold 마스크
			ok &= graph.Connect(perlinA.FindPort("height"), lerpNode.FindPort("a"));
			ok &= graph.Connect(perlinB.FindPort("height"), lerpNode.FindPort("b"));
			ok &= graph.Connect(thresholdNode.FindPort("height", PortDirection.Output), lerpNode.FindPort("t"));
			ok &= graph.Connect(lerpNode.FindPort("height"), outputNode.FindPort("height"));

			if (ok == false)
				Debug.LogError("[TerrainGraphMenu] Threshold+Lerp 데모 연결 실패 — port 타입/방향 검증 실패.");

			EditorUtility.SetDirty(graph);
			AssetDatabase.SaveAssets();
			Debug.Log($"[TerrainGraphMenu] ThresholdLerpDemoGraph 빌드 완료 — {graph.Nodes.Count} 노드, {graph.Connections.Count} 연결. Path: {DEMO_GRAPH_PATH}");
		}
	}
}
