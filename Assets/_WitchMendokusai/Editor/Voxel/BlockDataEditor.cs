using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// BlockData Inspector 확장 — 실제 voxel 셰이더(Texture2DArray) + worldScale 로 렌더한
	/// **1블록 3D 미리보기** (드래그 회전). texture/layer/worldScale 변경이 즉시 반영되어
	/// Play 없이 블록 외형을 iterate. mesher 와 동일한 face/UV/faceData 레이아웃을 미러링해
	/// 게임 내 렌더와 일치한다.
	/// </summary>
	[CustomEditor(typeof(BlockData))]
	public class BlockDataEditor : UnityEditor.Editor
	{
		private const string VOXEL_MATERIAL_PATH = "Assets/_WitchMendokusai/Domain/Voxel/Resources/VoxelMaterial.mat";
		private const string CONFIG_PATH = "Assets/_WitchMendokusai/Editor/Voxel/VoxelTextureConfig.asset";

		// mesher 와 동일 — Dirs[d] 0=Up 1=Down 2=Left 3=Right 4=Forward 5=Back.
		private static readonly Vector3[] Dirs = new Vector3[]
		{
			new(0, 1, 0), new(0, -1, 0), new(-1, 0, 0), new(1, 0, 0), new(0, 0, 1), new(0, 0, -1)
		};

		private static readonly Vector3[][] FaceVertices = new Vector3[][]
		{
			new Vector3[] { new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), new(0, 1, 0) }, // Up
			new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1) }, // Down
			new Vector3[] { new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0) }, // Left
			new Vector3[] { new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1) }, // Right
			new Vector3[] { new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1) }, // Forward
			new Vector3[] { new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0) }  // Back
		};

		private PreviewRenderUtility previewUtility;
		private Mesh previewMesh;
		private Material previewMaterial;
		private Vector2 previewDir = new(125f, -22f);

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			VoxelTextureConfig config = AssetDatabase.LoadAssetAtPath<VoxelTextureConfig>(CONFIG_PATH);
			BlockData block = target as BlockData;
			float worldScale = block.TextureWorldScale > 0f ? block.TextureWorldScale : 1f;
			string density = config != null
				? $"{Mathf.RoundToInt(config.Resolution / worldScale)} px / 블록"
				: "(VoxelTextureConfig 없음)";
			EditorGUILayout.HelpBox($"실효 텍셀 밀도 ≈ {density}  (Resolution ÷ WorldScale).\n아래 미리보기 = 실제 셰이더(Texture2DArray)로 렌더 — 드래그 회전. 텍스쳐 바꾼 뒤 'WM/Voxel/Build Voxel Texture Array' 재실행해야 layer 갱신됨.", MessageType.None);
		}

		public override bool HasPreviewGUI() => true;

		public override GUIContent GetPreviewTitle() => new("Block Preview");

		public override void OnPreviewGUI(Rect rect, GUIStyle background)
		{
			EnsurePreview();

			if (previewMaterial == null)
			{
				if (Event.current.type == EventType.Repaint)
					EditorGUI.DropShadowLabel(rect, "VoxelMaterial 없음 — 'WM/Voxel/Build Voxel Texture Array' 먼저 실행");
				return;
			}

			previewDir = Drag2D(previewDir, rect);

			if (Event.current.type != EventType.Repaint)
				return;

			RebuildMesh();

			previewUtility.BeginPreview(rect, background);
			Quaternion rotation = Quaternion.Euler(previewDir.y, previewDir.x, 0f);
			previewUtility.DrawMesh(previewMesh, Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one), previewMaterial, 0);
			previewUtility.camera.Render();
			Texture rendered = previewUtility.EndPreview();
			GUI.DrawTexture(rect, rendered, ScaleMode.StretchToFill, false);
		}

		private void EnsurePreview()
		{
			if (previewUtility == null)
			{
				previewUtility = new PreviewRenderUtility();
				previewUtility.cameraFieldOfView = 30f;
				previewUtility.camera.transform.position = new Vector3(0f, 0f, -3.2f);
				previewUtility.camera.transform.rotation = Quaternion.identity;
				previewUtility.camera.nearClipPlane = 0.1f;
				previewUtility.camera.farClipPlane = 50f;
				previewUtility.lights[0].intensity = 1.2f;
				previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 40f, 0f);
				if (previewUtility.lights.Length > 1)
					previewUtility.lights[1].intensity = 0.5f;
			}
			if (previewMaterial == null)
				previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(VOXEL_MATERIAL_PATH);
		}

		/// <summary>mesher 와 동일한 1블록 mesh 생성 (face/normal/uv/faceData). 매 Repaint 재빌드 — 24 vert, cheap.</summary>
		private void RebuildMesh()
		{
			BlockData block = target as BlockData;
			if (previewMesh == null)
				previewMesh = new Mesh { name = "BlockPreview" };
			previewMesh.Clear();

			Vector3[] vertices = new Vector3[24];
			Vector3[] normals = new Vector3[24];
			Color[] colors = new Color[24];
			Vector2[] uvs = new Vector2[24];
			List<Vector4> faceData = new(24);
			int[] triangles = new int[36];

			float worldScale = block.TextureWorldScale > 0f ? block.TextureWorldScale : 1f;
			float stochasticFlag = block.UseStochasticTiling ? 1f : 0f;

			for (int d = 0; d < 6; d++)
			{
				int layer = GetLayerForFace(block, d);
				Color faceColor = layer >= 0 ? Color.white : block.Color;
				Vector3 normal = Dirs[d];
				Vector4 perFace = new(layer, worldScale, stochasticFlag, 0f);

				for (int v = 0; v < 4; v++)
				{
					int index = (d * 4) + v;
					Vector3 corner = FaceVertices[d][v]; // 0..1 — 게임 worldUV 와 동일 좌표
					vertices[index] = corner - new Vector3(0.5f, 0.5f, 0.5f); // 원점 중심으로 이동 (미리보기 회전축)
					normals[index] = normal;
					colors[index] = faceColor;
					uvs[index] = GetWorldUV(d, corner);
					faceData.Add(perFace);
				}

				int triBase = d * 6;
				int vertBase = d * 4;
				triangles[triBase + 0] = vertBase + 0;
				triangles[triBase + 1] = vertBase + 1;
				triangles[triBase + 2] = vertBase + 2;
				triangles[triBase + 3] = vertBase + 0;
				triangles[triBase + 4] = vertBase + 2;
				triangles[triBase + 5] = vertBase + 3;
			}

			previewMesh.vertices = vertices;
			previewMesh.normals = normals;
			previewMesh.colors = colors;
			previewMesh.uv = uvs;
			previewMesh.SetUVs(1, faceData);
			previewMesh.triangles = triangles;
			previewMesh.RecalculateBounds();
		}

		private static int GetLayerForFace(BlockData block, int dirIndex)
		{
			if (dirIndex == 0)
				return block.TopLayer;
			if (dirIndex == 1)
				return block.BottomLayer;
			return block.SideLayer;
		}

		private static Vector2 GetWorldUV(int dirIndex, Vector3 corner)
		{
			if (dirIndex == 0 || dirIndex == 1)
				return new Vector2(corner.x, corner.z);
			if (dirIndex == 2 || dirIndex == 3)
				return new Vector2(corner.z, corner.y);
			return new Vector2(corner.x, corner.y);
		}

		private static Vector2 Drag2D(Vector2 rotation, Rect position)
		{
			int controlID = GUIUtility.GetControlID("BlockDataPreview".GetHashCode(), FocusType.Passive, position);
			Event current = Event.current;
			switch (current.GetTypeForControl(controlID))
			{
				case EventType.MouseDown:
					if (position.Contains(current.mousePosition) && position.width > 50f)
					{
						GUIUtility.hotControl = controlID;
						current.Use();
						EditorGUIUtility.SetWantsMouseJumping(1);
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlID)
						GUIUtility.hotControl = 0;
					EditorGUIUtility.SetWantsMouseJumping(0);
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlID)
					{
						rotation -= current.delta * (current.shift ? 3f : 1f) / Mathf.Min(position.width, position.height) * 140f;
						current.Use();
						GUI.changed = true;
					}
					break;
			}
			return rotation;
		}

		private void OnDisable()
		{
			if (previewUtility != null)
			{
				previewUtility.Cleanup();
				previewUtility = null;
			}
			if (previewMesh != null)
			{
				DestroyImmediate(previewMesh);
				previewMesh = null;
			}
		}
	}
}
