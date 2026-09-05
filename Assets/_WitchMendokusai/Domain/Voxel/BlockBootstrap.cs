using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 블록 목록을 <b>레지스트리에 올리는</b> 통로 (TASK-WM-409).
	///
	/// ★ 예전에는 <c>[RuntimeInitializeOnLoadMethod]</c> + <c>Resources.LoadAll("Blocks")</c> 였다.
	///   그러면 <b>복셀을 한 조각도 안 쓰는 제품</b>(방치형)에서도 블록·재질·텍스처 배열이
	///   빌드에 실리고 부팅 때 로드됐다 — 실측 2026-08-16: `VoxelTextureArray` 1.3MB.
	///   이제 <b>쓰는 쪽이 목록을 건네준다</b>(`ChunkManager` 의 `BlockCatalog`).
	///
	/// ⚠ 에디터에서는 Play 를 안 눌러도 블록을 물어보는 도구(도감·인스펙터)가 있어
	///   <b>에디터 한정</b>으로 자동 채움을 남긴다. 빌드에는 안 들어간다.
	/// </summary>
	public static class BlockBootstrap
	{
		/// <summary>쓰는 쪽이 부른다. 목록이 비면 그렇다고 말한다 — 조용히 빈 세계를 만들지 않는다.</summary>
		public static void Load(BlockData[] blocks)
		{
			if (blocks == null || blocks.Length == 0)
			{
				Debug.LogError("[BlockBootstrap] 블록 목록이 비었다 — BlockCatalog 배선을 확인할 것 (TASK-WM-409)");
				return;
			}
			BlockRegistry.Initialize(blocks);
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		private static void InitializeEditor()
		{
			ReloadFromProject();
		}

		/// <summary>에디터 전용 — 프로젝트 안의 모든 `BlockData` 를 긁어 레지스트리에 올린다.</summary>
		public static void ReloadFromProject()
		{
			string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BlockData");
			BlockData[] blocks = new BlockData[guids.Length];
			for (int i = 0; i < guids.Length; i++)
			{
				string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
				blocks[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockData>(path);
			}
			if (blocks.Length > 0) { BlockRegistry.Initialize(blocks); }
		}
#endif
	}
}
