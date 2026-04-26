using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 부팅 시 Resources/Blocks 폴더의 모든 BlockData를 로드해 BlockRegistry 초기화.
	/// Editor에서도 도메인 리로드마다 자동 호출.
	/// </summary>
	public static class BlockBootstrap
	{
		public const string RESOURCES_FOLDER = "Blocks";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void InitializeRuntime()
		{
			Reload();
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		private static void InitializeEditor()
		{
			Reload();
		}
#endif

		public static void Reload()
		{
			BlockData[] blocks = Resources.LoadAll<BlockData>(RESOURCES_FOLDER);
			BlockRegistry.Initialize(blocks);
		}
	}
}
