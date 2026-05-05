using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 에디터 EditorWindow. 디자이너용 thin host — 실제 View는 Runtime에 있는 TerrainEditorView.
	/// 메뉴: WitchMendokusai/Terrain Editor
	/// 상단 툴바에서 프리셋 선택/생성/복제/Reveal/Apply to Active 가능.
	/// </summary>
	public class TerrainEditorWindow : EditorWindow
	{
		private const string TERRAIN_FOLDER = "Assets/_WitchMendokusai/Core/Scripts/Terrain";
		private const string BIOMES_FOLDER = TERRAIN_FOLDER + "/Biomes";

		[MenuItem("WitchMendokusai/Terrain Editor")]
		public static void Open()
		{
			TerrainEditorWindow window = GetWindow<TerrainEditorWindow>();
			window.titleContent = new GUIContent("Terrain Editor");
			window.minSize = new Vector2(680, 360);
			window.Show();
		}

		private TerrainParameters editing;
		private TerrainParameters activeAsset;

		private VisualElement viewContainer;
		private ObjectField presetField;
		private Button applyToActiveButton;

		private PreviewRenderUtility previewUtility;
		private Material previewMaterial;

		// F2 — TwoPaneSplitView 우측 그래프 패널 + reference 변화 감지
		private TerrainEditorView terrainView;
		private VisualElement graphPaneContainer;
		private NodeGraph.NodeGraphView graphView;
		private TerrainGraph lastSeenGraph;
		// F2 후속 — graph SO dirty count polling (Inspector 안 노드 파라미터 변경 시 preview 자동 새로고침)
		private int lastGraphDirtyCount = -1;

		private void CreateGUI()
		{
			rootVisualElement.style.flexGrow = 1;

			activeAsset = TerrainParametersBootstrap.EnsureActive();
			editing = activeAsset;
			EnsureDefaultBiomes(editing);

			BuildToolbar();

			viewContainer = new VisualElement();
			viewContainer.style.flexGrow = 1;
			rootVisualElement.Add(viewContainer);

			BuildView();
		}

		private void OnDisable()
		{
			previewUtility?.Cleanup();
			previewUtility = null;
			if (previewMaterial != null)
			{
				DestroyImmediate(previewMaterial);
				previewMaterial = null;
			}
		}

		private void BuildToolbar()
		{
			VisualElement toolbar = new();
			toolbar.style.flexDirection = FlexDirection.Row;
			toolbar.style.alignItems = Align.Center;
			toolbar.style.paddingLeft = 6;
			toolbar.style.paddingRight = 6;
			toolbar.style.paddingTop = 6;
			toolbar.style.paddingBottom = 6;
			toolbar.style.borderBottomWidth = 1;
			toolbar.style.borderBottomColor = new Color(0f, 0f, 0f, 0.35f);
			rootVisualElement.Add(toolbar);

			presetField = new ObjectField("Preset")
			{
				objectType = typeof(TerrainParameters),
				value = editing,
				allowSceneObjects = false,
			};
			presetField.style.flexGrow = 1;
			presetField.RegisterValueChangedCallback(evt =>
			{
				TerrainParameters newPreset = evt.newValue as TerrainParameters;
				if (newPreset == null)
				{
					presetField.SetValueWithoutNotify(editing);
					return;
				}
				SwitchTo(newPreset);
			});
			toolbar.Add(presetField);

			Button newButton = new(NewPreset) { text = "New" };
			newButton.style.marginLeft = 4;
			toolbar.Add(newButton);

			Button saveAsButton = new(SaveAsPreset) { text = "Save As" };
			saveAsButton.style.marginLeft = 4;
			toolbar.Add(saveAsButton);

			Button revealButton = new(RevealPreset) { text = "Reveal" };
			revealButton.style.marginLeft = 4;
			toolbar.Add(revealButton);

			applyToActiveButton = new Button(ApplyToActive) { text = "Apply to Active" };
			applyToActiveButton.style.marginLeft = 4;
			toolbar.Add(applyToActiveButton);

			UpdateApplyButtonState();
		}

		private void BuildView()
		{
			viewContainer.Clear();

			// 좌: TerrainEditorView (사이드바 + preview), 우: NodeGraphView (그래프 편집)
			// fixedPaneIndex=0 (좌측 고정 600px), 우측은 flex.
			TwoPaneSplitView splitView = new(0, 600, TwoPaneSplitViewOrientation.Horizontal);

			terrainView = new TerrainEditorView(editing, OnParameterChanged, RenderMesh3D);
			splitView.Add(terrainView);

			graphPaneContainer = new VisualElement { style = { flexGrow = 1 } };
			splitView.Add(graphPaneContainer);

			viewContainer.Add(splitView);

			RebuildGraphPane();
		}

		private void RebuildGraphPane()
		{
			if (graphPaneContainer == null)
				return;
			graphPaneContainer.Clear();
			graphView = null;
			lastSeenGraph = editing != null ? editing.TerrainGraph : null;

			if (editing == null || editing.TerrainGraph == null)
			{
				Label hint = new("그래프 미할당\n\nWitchMendokusai → Terrain → Build Default Terrain Graph")
				{
					style =
					{
						color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)),
						alignSelf = Align.Center,
						marginTop = 60,
						unityTextAlign = TextAnchor.MiddleCenter,
						whiteSpace = WhiteSpace.Normal,
					}
				};
				graphPaneContainer.Add(hint);
				return;
			}

			graphView = new NodeGraph.NodeGraphView(editing.TerrainGraph);
			graphView.style.flexGrow = 1;
			graphPaneContainer.Add(graphView);
		}

		private void OnFocus()
		{
			// 사용자가 외부에서 (Inspector / 'Build Default Terrain Graph' 메뉴) terrainGraph 슬롯 변경 시 detect.
			if (editing == null)
				return;
			if (editing.TerrainGraph != lastSeenGraph)
				RebuildGraphPane();
		}

		/// <summary>
		/// Editor 가 초당 ~10회 호출. graph SO 의 dirty count 가 바뀌면 (사용자가 Inspector 에서 노드 필드
		/// 편집 등) terrainView preview 새로고침. 통합 창 UX — "그래프 편집 → 즉시 시각 반영" 자연.
		/// </summary>
		private void OnInspectorUpdate()
		{
			if (terrainView == null || editing == null || editing.TerrainGraph == null)
				return;
			int dirty = EditorUtility.GetDirtyCount(editing.TerrainGraph);
			if (dirty == lastGraphDirtyCount)
				return;
			lastGraphDirtyCount = dirty;
			terrainView.Refresh();
		}

		private void OnParameterChanged()
		{
			if (editing != null)
				EditorUtility.SetDirty(editing);
		}

		private void UpdateApplyButtonState()
		{
			bool isEditingActive = editing == activeAsset;
			applyToActiveButton.SetEnabled(isEditingActive == false);
			applyToActiveButton.tooltip = isEditingActive
				? "이미 Active 프리셋 편집 중 — 자동 반영"
				: "현재 프리셋 값을 Active로 복사 (런타임이 사용)";
		}

		private void SwitchTo(TerrainParameters preset)
		{
			editing = preset;
			EnsureDefaultBiomes(editing);
			presetField.SetValueWithoutNotify(preset);
			BuildView();
			UpdateApplyButtonState();
		}

		private void NewPreset()
		{
			string path = EditorUtility.SaveFilePanelInProject(
				"New Terrain Preset",
				"TerrainParameters_New",
				"asset",
				"새 프리셋 .asset 위치 선택",
				TERRAIN_FOLDER);
			if (string.IsNullOrEmpty(path))
				return;

			TerrainParameters created = CreateInstance<TerrainParameters>();
			AssetDatabase.CreateAsset(created, path);
			AssetDatabase.SaveAssets();
			SwitchTo(created);
		}

		private void SaveAsPreset()
		{
			if (editing == null)
				return;

			string path = EditorUtility.SaveFilePanelInProject(
				"Save Preset As",
				$"{editing.name}_Copy",
				"asset",
				"프리셋 복제 위치 선택",
				TERRAIN_FOLDER);
			if (string.IsNullOrEmpty(path))
				return;

			TerrainParameters copy = CreateInstance<TerrainParameters>();
			EditorUtility.CopySerialized(editing, copy);
			copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
			AssetDatabase.CreateAsset(copy, path);
			AssetDatabase.SaveAssets();
			SwitchTo(copy);
		}

		private void RevealPreset()
		{
			if (editing == null)
				return;
			EditorGUIUtility.PingObject(editing);
			Selection.activeObject = editing;
		}

		private void ApplyToActive()
		{
			if (editing == null || activeAsset == null)
				return;
			if (editing == activeAsset)
				return;

			string sourceName = editing.name;
			string preservedName = activeAsset.name;
			EditorUtility.CopySerialized(editing, activeAsset);
			activeAsset.name = preservedName;
			EditorUtility.SetDirty(activeAsset);
			AssetDatabase.SaveAssets();
			TerrainParametersService.ClearCache();
			Debug.Log($"[Terrain Editor] Active 프리셋이 '{sourceName}' 값으로 갱신됨.");
		}

		private Texture RenderMesh3D(Mesh mesh, Vector2 rotation, float zoom, int width, int height)
		{
			EnsurePreviewUtility();

			Rect rect = new(0, 0, Mathf.Max(1, width), Mathf.Max(1, height));

			previewUtility.BeginPreview(rect, GUIStyle.none);

			Bounds bounds = mesh.bounds;
			Vector3 center = bounds.center;
			float baseDistance = Mathf.Max(bounds.extents.magnitude * 2.2f, 8f);
			float distance = baseDistance / Mathf.Max(zoom, 0.0001f);

			Quaternion orbit = Quaternion.Euler(rotation.x, rotation.y, 0f);
			Vector3 cameraOffset = orbit * (Vector3.back * distance);

			previewUtility.camera.transform.position = center + cameraOffset;
			previewUtility.camera.transform.LookAt(center);
			previewUtility.camera.nearClipPlane = 0.1f;
			previewUtility.camera.farClipPlane = distance * 4f;
			previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
			previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;

			previewUtility.lights[0].intensity = 1.2f;
			previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);

			previewUtility.DrawMesh(mesh, Matrix4x4.identity, previewMaterial, 0);
			previewUtility.camera.Render();

			Texture rendered = previewUtility.EndPreview();
			return rendered;
		}

		private void EnsurePreviewUtility()
		{
			if (previewUtility == null)
				previewUtility = new PreviewRenderUtility();

			if (previewMaterial == null)
			{
				Shader shader = Shader.Find("WM/VoxelVertexColor");
				if (shader == null)
					shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
				if (shader == null)
					shader = Shader.Find("Standard");
				previewMaterial = new Material(shader);
			}
		}

		private static void EnsureDefaultBiomes(TerrainParameters parameters)
		{
			if (parameters == null)
				return;

			if (parameters.Biomes != null && parameters.Biomes.Count > 0)
			{
				BackfillVoxelBlocks(parameters);
				return;
			}

			if (AssetDatabase.IsValidFolder(BIOMES_FOLDER) == false)
				AssetDatabase.CreateFolder(TERRAIN_FOLDER, "Biomes");

			BlockData grass = LoadBlock("wm_grass");
			BlockData dirt = LoadBlock("wm_dirt");
			BlockData stone = LoadBlock("wm_stone");

			BiomeData forest = LoadOrCreateBiome("Forest", new Color(0.20f, 0.55f, 0.20f, 1f), grass, dirt, 3);
			BiomeData plains = LoadOrCreateBiome("Plains", new Color(0.55f, 0.78f, 0.40f, 1f), grass, dirt, 3);
			BiomeData mountain = LoadOrCreateBiome("Mountain", new Color(0.55f, 0.55f, 0.55f, 1f), stone, stone, 4);

			List<BiomeWeight> weights = new()
			{
				new BiomeWeight(forest, 0.5f),
				new BiomeWeight(plains, 0.3f),
				new BiomeWeight(mountain, 0.2f),
			};
			parameters.SetBiomes(weights);
			EditorUtility.SetDirty(parameters);
			AssetDatabase.SaveAssets();
		}

		/// <summary>이미 생성된 BiomeData에 voxel 블록 필드가 비어있으면 채워준다 (구버전 호환).</summary>
		private static void BackfillVoxelBlocks(TerrainParameters parameters)
		{
			BlockData grass = LoadBlock("wm_grass");
			BlockData dirt = LoadBlock("wm_dirt");
			BlockData stone = LoadBlock("wm_stone");

			foreach (BiomeWeight bw in parameters.Biomes)
			{
				BiomeData biome = bw.Biome;
				if (biome == null)
					continue;

				bool dirty = false;
				if (biome.SurfaceBlock == null)
				{
					BlockData surface = biome.BiomeName == "Mountain" ? stone : grass;
					biome.SetSurfaceBlock(surface);
					dirty = true;
				}
				if (biome.SubsurfaceBlock == null)
				{
					BlockData subsurface = biome.BiomeName == "Mountain" ? stone : dirt;
					biome.SetSubsurfaceBlock(subsurface);
					dirty = true;
				}
				if (dirty)
					EditorUtility.SetDirty(biome);
			}
			AssetDatabase.SaveAssets();
		}

		private static BlockData LoadBlock(string fileName)
		{
			string path = $"Assets/_WitchMendokusai/Core/Scripts/Voxel/Resources/Blocks/{fileName}.asset";
			return AssetDatabase.LoadAssetAtPath<BlockData>(path);
		}

		private static BiomeData LoadOrCreateBiome(string biomeName, Color color, BlockData surface, BlockData subsurface, int subsurfaceDepth)
		{
			string path = $"{BIOMES_FOLDER}/{biomeName}.asset";
			BiomeData existing = AssetDatabase.LoadAssetAtPath<BiomeData>(path);
			if (existing != null)
				return existing;

			BiomeData created = CreateInstance<BiomeData>();
			created.SetBiomeName(biomeName);
			created.SetPreviewColor(color);
			created.SetSurfaceBlock(surface);
			created.SetSubsurfaceBlock(subsurface);
			created.SetSubsurfaceDepth(subsurfaceDepth);
			AssetDatabase.CreateAsset(created, path);
			return created;
		}
	}
}
