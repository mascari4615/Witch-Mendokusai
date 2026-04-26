using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 에디터 EditorWindow. 디자이너용 thin host — 실제 View는 Runtime에 있는 TerrainEditorView.
	/// 메뉴: WitchMendokusai/Terrain Editor
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

		private TerrainParameters parameters;
		private PreviewRenderUtility previewUtility;
		private Material previewMaterial;

		private void CreateGUI()
		{
			rootVisualElement.style.flexGrow = 1;
			parameters = ResolveParameters();
			EnsureDefaultBiomes(parameters);
			rootVisualElement.Add(new TerrainEditorView(parameters, OnParameterChanged, RenderMesh3D));
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

		private void OnParameterChanged()
		{
			if (parameters != null)
				EditorUtility.SetDirty(parameters);
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

		private static TerrainParameters ResolveParameters()
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(TerrainParameters)}");
			if (guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				return AssetDatabase.LoadAssetAtPath<TerrainParameters>(path);
			}

			TerrainParameters created = CreateInstance<TerrainParameters>();
			string assetPath = $"{TERRAIN_FOLDER}/{nameof(TerrainParameters)}_Default.asset";
			AssetDatabase.CreateAsset(created, assetPath);
			AssetDatabase.SaveAssets();
			return created;
		}

		private static void EnsureDefaultBiomes(TerrainParameters parameters)
		{
			if (parameters.Biomes != null && parameters.Biomes.Count > 0)
				return;

			if (!AssetDatabase.IsValidFolder(BIOMES_FOLDER))
				AssetDatabase.CreateFolder(TERRAIN_FOLDER, "Biomes");

			BiomeData forest = LoadOrCreateBiome("Forest", new Color(0.20f, 0.55f, 0.20f, 1f));
			BiomeData plains = LoadOrCreateBiome("Plains", new Color(0.55f, 0.78f, 0.40f, 1f));
			BiomeData mountain = LoadOrCreateBiome("Mountain", new Color(0.55f, 0.55f, 0.55f, 1f));

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

		private static BiomeData LoadOrCreateBiome(string biomeName, Color color)
		{
			string path = $"{BIOMES_FOLDER}/{biomeName}.asset";
			BiomeData existing = AssetDatabase.LoadAssetAtPath<BiomeData>(path);
			if (existing != null)
				return existing;

			BiomeData created = CreateInstance<BiomeData>();
			created.SetBiomeName(biomeName);
			created.SetPreviewColor(color);
			AssetDatabase.CreateAsset(created, path);
			return created;
		}
	}
}
