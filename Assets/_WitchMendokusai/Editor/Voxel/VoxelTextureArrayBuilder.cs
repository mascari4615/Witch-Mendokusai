using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Voxel 텍스쳐 Array 빌더. **모든 BlockData asset 을 스캔**해 그들의 Side/Top/Bottom Texture2D 를
	/// 단일 **Texture2DArray** 로 패킹 + 각 BlockData 에 layer index 를 직접 박는다 (직렬화).
	/// 같은 Texture2D 는 dedup — 1 layer 공유. 결정성: BlockData identifier 알파벳 순 + face 순(side/top/bottom).
	///
	/// Atlas (구) 대비 근본 이점:
	/// - 각 텍스쳐가 독립 layer → **임의 고해상도** (atlas 타일 크기 캡 없음).
	/// - layer 단위 **mipmap** 생성 가능 (atlas 는 타일 bleed 로 불가) → 원거리 알리아싱 제거.
	/// - 하드웨어 **Repeat wrap** → worldspace seamless 타일링 (셰이더 frac 핵 불요, mip 경계 정상).
	/// 셰이더는 `worldUV / worldScale` 를 그대로 uv 로 쓰고 layer 로 array 샘플.
	/// </summary>
	public static class VoxelTextureArrayBuilder
	{
		public const string CONFIG_PATH = "Assets/_WitchMendokusai/Editor/Voxel/VoxelTextureConfig.asset";
		public const string ARRAY_ASSET_PATH = "Assets/_WitchMendokusai/Domain/Voxel/Scripts/Resources/VoxelTextureArray.asset";
		public const string VOXEL_SHADER_NAME = "WM/VoxelVertexColor";
		public const string MATERIAL_TEXTURE_PROPERTY = "_MainTex";

		private enum BlockFace { Side, Top, Bottom }

		[MenuItem("WM/Voxel/Open Voxel Texture Settings")]
		public static void OpenTextureConfig()
		{
			VoxelTextureConfig config = LoadOrCreateConfig();
			Selection.activeObject = config;
			EditorGUIUtility.PingObject(config);
			Debug.Log($"[VoxelTextureArrayBuilder] 설정 = {CONFIG_PATH} (Inspector 에서 Resolution / Filter 조정 후 'WM/Voxel/Build Voxel Texture Array' 재실행).");
		}

		[MenuItem("WM/Voxel/Build Voxel Texture Array")]
		public static void BuildTextureArray()
		{
			BlockData[] blocks = LoadAllBlockDataSorted();
			if (blocks.Length == 0)
			{
				Debug.LogWarning("[VoxelTextureArrayBuilder] BlockData asset 없음. VoxelBootstrap.GenerateDefaultBlocks 먼저 실행.");
				return;
			}

			VoxelTextureConfig config = LoadOrCreateConfig();
			int resolution = config.Resolution;

			// 1) 고유 텍스쳐 → layer index 결정 (결정적 순서).
			Dictionary<Texture2D, int> textureToLayer = new();
			List<Texture2D> orderedTextures = new();
			foreach (BlockData block in blocks)
			{
				CollectFaceTexture(block, BlockFace.Side, textureToLayer, orderedTextures);
				CollectFaceTexture(block, BlockFace.Top, textureToLayer, orderedTextures);
				CollectFaceTexture(block, BlockFace.Bottom, textureToLayer, orderedTextures);
			}

			// 2) Texture2DArray 생성 (최소 depth 1 — 텍스쳐 0 개여도 빈 sampler 회피).
			int depth = Mathf.Max(1, orderedTextures.Count);
			Texture2DArray array = new(resolution, resolution, depth, TextureFormat.RGBA32, config.GenerateMipmaps)
			{
				filterMode = config.Filter,
				wrapMode = TextureWrapMode.Repeat,
				anisoLevel = config.AnisoLevel,
				name = "VoxelTextureArray"
			};

			if (orderedTextures.Count == 0)
			{
				// 텍스쳐 미할당 — 흰색 더미 layer 1개 (셰이더 sentinel path 가 lerp out 하지만 sampler 안전성).
				Color32[] white = Enumerable.Repeat(new Color32(255, 255, 255, 255), resolution * resolution).ToArray();
				array.SetPixels32(white, 0, 0);
			}
			else
			{
				for (int layer = 0; layer < orderedTextures.Count; layer++)
				{
					Color32[] pixels = ResampleToResolution(orderedTextures[layer], resolution);
					array.SetPixels32(pixels, layer, 0);
					Debug.Log($"[VoxelTextureArrayBuilder] layer {layer} ← {AssetDatabase.GetAssetPath(orderedTextures[layer])} ({orderedTextures[layer].width}×{orderedTextures[layer].height} → {resolution}×{resolution})");
				}
			}

			array.Apply(updateMipmaps: config.GenerateMipmaps, makeNoLongerReadable: false);

			// 3) asset 저장 (기존 있으면 교체).
			if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(ARRAY_ASSET_PATH) != null)
				AssetDatabase.DeleteAsset(ARRAY_ASSET_PATH);
			AssetDatabase.CreateAsset(array, ARRAY_ASSET_PATH);

			// 4) 각 BlockData 에 layer index 박기.
			foreach (BlockData block in blocks)
			{
				AssignFaceLayer(block, BlockFace.Side, textureToLayer);
				AssignFaceLayer(block, BlockFace.Top, textureToLayer);
				AssignFaceLayer(block, BlockFace.Bottom, textureToLayer);
				EditorUtility.SetDirty(block);
			}

			// 5) 모든 voxel material 의 _MainTex 에 array wire.
			WireArrayToAllVoxelMaterials(array);

			AssetDatabase.SaveAssets();

			Debug.Log($"[VoxelTextureArrayBuilder] Texture Array built: {blocks.Length} blocks, {orderedTextures.Count} 고유 텍스쳐, {resolution}px, mip={config.GenerateMipmaps}, filter={config.Filter} → {ARRAY_ASSET_PATH}");
		}

		private static BlockData[] LoadAllBlockDataSorted()
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(BlockData)}");
			return guids
				.Select(AssetDatabase.GUIDToAssetPath)
				.Select(AssetDatabase.LoadAssetAtPath<BlockData>)
				.Where(block => block != null)
				.OrderBy(block => block.Identifier)
				.ToArray();
		}

		/// <summary>명시 할당된 face 텍스쳐를 dedup 하여 layer 순서 목록에 등록.</summary>
		private static void CollectFaceTexture(BlockData block, BlockFace face, Dictionary<Texture2D, int> textureToLayer, List<Texture2D> orderedTextures)
		{
			Texture2D faceTexture = GetFaceTextureRaw(block, face);
			if (faceTexture == null)
				return;
			if (textureToLayer.ContainsKey(faceTexture))
				return;
			textureToLayer[faceTexture] = orderedTextures.Count;
			orderedTextures.Add(faceTexture);
		}

		private static void AssignFaceLayer(BlockData block, BlockFace face, Dictionary<Texture2D, int> textureToLayer)
		{
			Texture2D faceTexture = GetFaceTextureRaw(block, face);
			int layer = -1;
			if (faceTexture != null && textureToLayer.TryGetValue(faceTexture, out int found))
				layer = found;

			switch (face)
			{
				case BlockFace.Side: block.SetSideLayer(layer); break;
				case BlockFace.Top: block.SetTopLayer(layer); break;
				case BlockFace.Bottom: block.SetBottomLayer(layer); break;
			}
		}

		/// <summary>fallback 적용 안 한 raw 필드 텍스쳐 — Builder 가 *명시 할당된* 면만 패킹하도록.</summary>
		private static Texture2D GetFaceTextureRaw(BlockData block, BlockFace face)
		{
			SerializedObject serialized = new(block);
			string fieldName = face switch
			{
				BlockFace.Side => "sideTexture",
				BlockFace.Top => "topTexture",
				BlockFace.Bottom => "bottomTexture",
				_ => null
			};
			if (fieldName == null)
				return null;
			SerializedProperty property = serialized.FindProperty(fieldName);
			return property?.objectReferenceValue as Texture2D;
		}

		/// <summary>입력 텍스쳐를 resolution×resolution 으로 GPU 리샘플 (bilinear, up/down 양방향 품질).
		/// RenderTexture blit → ReadPixels — 소스 readable 불요. sRGB 보존.</summary>
		private static Color32[] ResampleToResolution(Texture2D source, int resolution)
		{
			RenderTexture renderTexture = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture previousActive = RenderTexture.active;
			Graphics.Blit(source, renderTexture);
			RenderTexture.active = renderTexture;

			Texture2D readback = new(resolution, resolution, TextureFormat.RGBA32, false);
			readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
			readback.Apply();
			Color32[] pixels = readback.GetPixels32();

			RenderTexture.active = previousActive;
			RenderTexture.ReleaseTemporary(renderTexture);
			Object.DestroyImmediate(readback);
			return pixels;
		}

		/// <summary>
		/// 프로젝트 내 *모든* `WM/VoxelVertexColor` 셰이더 사용 material 의 `_MainTex` 에 빌드된 array 박기.
		/// 단일 정본 material 외 씬·프리팹 직접 생성 material 도 자동 wire — 빌드 1회로 동기화.
		/// </summary>
		public static void WireArrayToAllVoxelMaterials(Texture2DArray array)
		{
			string[] guids = AssetDatabase.FindAssets("t:Material");
			int wired = 0;
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (material == null || material.shader == null)
					continue;
				if (material.shader.name != VOXEL_SHADER_NAME)
					continue;
				if (material.HasProperty(MATERIAL_TEXTURE_PROPERTY) == false)
					continue;
				material.SetTexture(MATERIAL_TEXTURE_PROPERTY, array);
				EditorUtility.SetDirty(material);
				wired++;
				Debug.Log($"[VoxelTextureArrayBuilder] {path} ← array wire");
			}

			if (wired == 0)
				Debug.LogWarning($"[VoxelTextureArrayBuilder] '{VOXEL_SHADER_NAME}' 셰이더 사용 material 없음 — VoxelBootstrap.GenerateDefaultMaterial 먼저 실행.");
			else
				Debug.Log($"[VoxelTextureArrayBuilder] {wired} material array wire 완료.");
		}

		private static VoxelTextureConfig LoadOrCreateConfig()
		{
			VoxelTextureConfig config = AssetDatabase.LoadAssetAtPath<VoxelTextureConfig>(CONFIG_PATH);
			if (config != null)
				return config;

			config = ScriptableObject.CreateInstance<VoxelTextureConfig>();
			AssetDatabase.CreateAsset(config, CONFIG_PATH);
			AssetDatabase.SaveAssets();
			Debug.Log($"[VoxelTextureArrayBuilder] VoxelTextureConfig 자동 생성 → {CONFIG_PATH} (기본 512px, mip on, Trilinear). Inspector 에서 조정 가능.");
			return config;
		}
	}
}
