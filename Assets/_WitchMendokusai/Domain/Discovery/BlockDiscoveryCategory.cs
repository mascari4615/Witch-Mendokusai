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
	public class BlockDiscoveryCategory : IEntryProvider
	{
		public string Id => "block";
		public string DisplayName => "블록";
		public Sprite Icon => null;
		public IReadOnlyList<string> SubGroups => null;

		private readonly List<EntryDescriptor> entries = new();
		private readonly Dictionary<string, Sprite> spriteCache = new();
		private static readonly Dictionary<string, GameObject> previewPrefabCache = new();

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
				GameObject previewPrefab = ResolvePreviewPrefab(block);

				entries.Add(new EntryDescriptor(
					id: block.Identifier,
					displayName: block.BlockName,
					icon: icon,
					source: block,
					previewPrefab: previewPrefab));
			}
		}

		private GameObject ResolvePreviewPrefab(BlockData block)
		{
			if (previewPrefabCache.TryGetValue(block.Identifier, out GameObject cached) && cached != null)
				return cached;

			// TASK-WM-208 — 셰이더 이름을 여기서 문자열로 직접 부르면 「항상 포함할 셰이더」 대조를 안 받는다.
			// 화면이 멀쩡해 보이므로 영원히 안 걸리다가, 목록에서 빠지는 순간 **빌드에서만** 죽는다. 공용 입구 경유.
			GameObject cube = CombatPrimitive.Create(PrimitiveType.Cube, unlit: true);
			cube.name = $"BlockPreview_{block.Identifier}";
			cube.SetActive(false);
			Object.DontDestroyOnLoad(cube);

			Material material = cube.GetComponent<Renderer>().sharedMaterial;
			if (block.SideTexture != null)
				material.SetTexture("_BaseMap", block.SideTexture);
			else
				material.SetColor("_BaseColor", block.Color);

			previewPrefabCache[block.Identifier] = cube;
			return cube;
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

		public IReadOnlyList<EntryDescriptor> GetEntries() => entries;

		public VisualElement BuildDetail(EntryDescriptor entry)
		{
			BlockData block = entry.Source as BlockData;
			VisualElement detail = new();

			if (block == null)
				return detail;

			Label identifierLabel = new($"식별자: {block.Identifier}");
			detail.Add(identifierLabel);

			Label propsLabel = new($"solid: {block.IsSolid}  ·  opaque: {block.IsOpaque}");
			detail.Add(propsLabel);

			return detail;
		}
	}
}
