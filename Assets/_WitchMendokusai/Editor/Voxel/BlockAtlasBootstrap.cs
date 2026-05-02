using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 활성 BlockTextureAtlas .asset 이 Resources 에 항상 존재하도록 보장.
	/// 에디터 시작 시 자동 검사 + Builder 가 직접 호출 가능.
	/// </summary>
	[InitializeOnLoad]
	public static class BlockAtlasBootstrap
	{
		public const string RESOURCES_FOLDER = "Assets/_WitchMendokusai/Core/Scripts/Voxel/Resources";
		public const string ACTIVE_PATH = RESOURCES_FOLDER + "/BlockTextureAtlas_Active.asset";

		static BlockAtlasBootstrap()
		{
			EditorApplication.delayCall += () =>
			{
				BlockTextureAtlas atlas = EnsureActive();
				// atlas 에 이미 build 결과가 있으면 material 자동 wire (Builder 안 돌려도 atlas → material 연결).
				if (atlas != null && atlas.AtlasTexture != null)
					BlockAtlasBuilder.WireAtlasToMaterial(atlas.AtlasTexture);
			};
		}

		public static BlockTextureAtlas EnsureActive()
		{
			BlockTextureAtlas existing = AssetDatabase.LoadAssetAtPath<BlockTextureAtlas>(ACTIVE_PATH);
			if (existing != null)
				return existing;

			EnsureFolder(RESOURCES_FOLDER);

			BlockTextureAtlas created = ScriptableObject.CreateInstance<BlockTextureAtlas>();
			AssetDatabase.CreateAsset(created, ACTIVE_PATH);
			AssetDatabase.SaveAssets();
			Debug.Log($"[BlockAtlasBootstrap] Active atlas SO created at {ACTIVE_PATH}");
			return created;
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
