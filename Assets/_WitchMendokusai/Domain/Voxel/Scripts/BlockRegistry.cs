using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 모든 BlockData를 모아 RuntimeId를 부여하는 글로벌 레지스트리.
	/// Air = RuntimeId 0 보장. 그 외 블록은 Identifier 사전순으로 1, 2, 3...
	/// 부팅 시 Initialize(blocks) 호출 필요. RuntimeId는 직렬화 안 됨 — 세이브에는 Identifier(palette) 사용.
	/// </summary>
	public static class BlockRegistry
	{
		private static readonly Dictionary<string, BlockData> byIdentifier = new();
		private static readonly List<BlockData> byRuntimeId = new();

		public static int Count => byRuntimeId.Count;
		public static IReadOnlyList<BlockData> All => byRuntimeId;
		public static bool IsInitialized => byRuntimeId.Count > 0;

		public static void Initialize(IEnumerable<BlockData> blocks)
		{
			byIdentifier.Clear();
			byRuntimeId.Clear();

			List<BlockData> sourceList = blocks.Where(b => b != null).ToList();

			BlockData air = sourceList.FirstOrDefault(b => b.Identifier == VoxelConstants.AIR_IDENTIFIER);
			if (air == null)
			{
				Debug.LogError($"[BlockRegistry] Air block ({VoxelConstants.AIR_IDENTIFIER}) not found. Other blocks not registered.");
				return;
			}

			RegisterInternal(air);

			IEnumerable<BlockData> rest = sourceList
				.Where(b => b != air && byIdentifier.ContainsKey(b.Identifier) == false)
				.OrderBy(b => b.Identifier, System.StringComparer.Ordinal);

			foreach (BlockData block in rest)
				RegisterInternal(block);
		}

		private static void RegisterInternal(BlockData block)
		{
			if (byIdentifier.ContainsKey(block.Identifier))
			{
				Debug.LogWarning($"[BlockRegistry] Duplicate identifier ignored: {block.Identifier}");
				return;
			}

			ushort runtimeId = (ushort)byRuntimeId.Count;
			block.AssignRuntimeId(runtimeId);
			byIdentifier[block.Identifier] = block;
			byRuntimeId.Add(block);
		}

		public static BlockData GetByRuntimeId(ushort runtimeId)
		{
			if (runtimeId >= byRuntimeId.Count)
				return null;
			return byRuntimeId[runtimeId];
		}

		public static BlockData GetByIdentifier(string identifier)
		{
			byIdentifier.TryGetValue(identifier, out BlockData block);
			return block;
		}
	}
}
