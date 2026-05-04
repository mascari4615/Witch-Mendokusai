using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 블록 카테고리. BlockData 는 DataSO 아님 → BlockRegistry.All 직접 순회.
	/// Air 블록은 게임상 의미 없어 제외.
	/// 골격: Icon 은 null (BlockData 가 Texture2D 만 보유, Sprite 변환은 후속 TASK).
	/// </summary>
	public class BlockCodexCategory : ICodexCategory
	{
		public string Id => "block";
		public string DisplayName => "블록";
		public Sprite Icon => null;

		private readonly List<CodexEntry> entries = new();

		public void OnActivate()
		{
			entries.Clear();

			IReadOnlyList<BlockData> all = BlockRegistry.All;
			for (int i = 0; i < all.Count; i++)
			{
				BlockData block = all[i];
				if (block == null || block.IsAir)
					continue;

				entries.Add(new CodexEntry(
					id: block.Identifier,
					displayName: block.BlockName,
					icon: null,
					source: block));
			}
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
