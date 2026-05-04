using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 블록 카테고리. BlockData 는 DataSO 아님 → BlockRegistry.All 직접 순회.
	/// Air 블록은 게임상 의미 없어 제외.
	/// SideTexture → Sprite 변환 후 캐시 (instance dict). 같은 블록 재진입 시 같은 Sprite 재사용.
	/// </summary>
	public class BlockCodexCategory : ICodexCategory
	{
		public string Id => "block";
		public string DisplayName => "블록";
		public Sprite Icon => null;
		public IReadOnlyList<string> SubGroups => null;

		private readonly List<CodexEntry> entries = new();
		private readonly Dictionary<string, Sprite> spriteCache = new();

		public void OnActivate()
		{
			entries.Clear();

			IReadOnlyList<BlockData> all = BlockRegistry.All;
			for (int i = 0; i < all.Count; i++)
			{
				BlockData block = all[i];
				if (block == null || block.IsAir)
					continue;

				Sprite icon = ResolveIcon(block);

				entries.Add(new CodexEntry(
					id: block.Identifier,
					displayName: block.BlockName,
					icon: icon,
					source: block));
			}
		}

		private Sprite ResolveIcon(BlockData block)
		{
			if (spriteCache.TryGetValue(block.Identifier, out Sprite cached))
				return cached;

			Texture2D texture = block.SideTexture;
			if (texture == null)
			{
				spriteCache[block.Identifier] = null;
				return null;
			}

			Sprite sprite = Sprite.Create(
				texture,
				new Rect(0, 0, texture.width, texture.height),
				new Vector2(0.5f, 0.5f),
				100f);
			spriteCache[block.Identifier] = sprite;
			return sprite;
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<CodexEntry> GetEntries() => entries;

		public VisualElement BuildDetail(CodexEntry entry)
		{
			BlockData block = entry.Source as BlockData;
			VisualElement detail = new();

			Label nameLabel = new(block != null ? block.BlockName : entry.DisplayName);
			detail.Add(nameLabel);

			if (block != null)
			{
				Label identifierLabel = new($"id: {block.Identifier}");
				detail.Add(identifierLabel);

				Label solidLabel = new($"solid: {block.IsSolid}  /  opaque: {block.IsOpaque}");
				detail.Add(solidLabel);
			}

			return detail;
		}
	}
}
