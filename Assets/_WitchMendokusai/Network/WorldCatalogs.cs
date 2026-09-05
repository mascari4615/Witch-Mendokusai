using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{

	/// <summary>
	/// 내 안의 세계가 아는 아이템 목록 (TASK-WM-217).
	///
	/// <b>정본은 게임 자산</b>이고, 에디터에서 뽑아 둔 목록(<c>Resources/items.json</c>)을 읽는다 —
	/// 서버가 읽는 것과 <b>같은 파일 모양</b>이라 혼자 놀 때와 같이 놀 때가 갈라지지 않는다.
	/// 목록이 아직 없으면(뽑기 전) 아무 것도 모르는 세계가 된다 — 조용히 씨앗으로 때우면
	/// 「왜 이 아이템만 안 들어가지」로 나중에 나타난다.
	/// </summary>
	public static class ItemCatalog
	{
		private const string RESOURCE_NAME = "items";

		private static WorldItemCatalog catalog;

		public static IItemData Find(int itemId) => Ensure().Find(itemId);

		/// <summary>목록 그대로 — 상자 안을 되살릴 때 「이 번호가 무엇인가」를 알아야 한다.</summary>
		public static WorldItemCatalog Loaded => Ensure();

		private static WorldItemCatalog Ensure()
		{
			if (catalog != null)
				return catalog;

			TextAsset asset = Resources.Load<TextAsset>(RESOURCE_NAME);
			if (asset == null)
			{
				Debug.LogWarning("[items] Resources/items.json 이 없다 — WM > 아이템 목록 뽑기 를 한 번 돌릴 것.");
				catalog = new WorldItemCatalog(null);
				return catalog;
			}

			catalog = new WorldItemCatalog(JsonUtility.FromJson<ItemCatalogData>(asset.text));
			return catalog;
		}
	}

	/// <summary>
	/// 내 안의 세계가 아는 <b>지을 것</b> 목록 (TASK-WM-217) — 아이템 목록과 같은 방식.
	/// 뽑아 둔 <c>Resources/buildings.json</c> 이 있으면 그것을, 없으면 씨앗으로.
	/// </summary>
	public static class BuildingCatalog
	{
		private const string RESOURCE_NAME = "buildings";

		private static WorldBuildingCatalog catalog;

		public static WorldBuildingCatalog Loaded => catalog ?? (catalog = Load());

		private static WorldBuildingCatalog Load()
		{
			TextAsset asset = Resources.Load<TextAsset>(RESOURCE_NAME);
			if (asset == null)
			{
				Debug.LogWarning("[buildings] Resources/buildings.json 이 없다 — WM > 아이템 목록 뽑기 를 한 번 돌릴 것(씨앗으로 돈다).");
				return new WorldBuildingCatalog(WorldSeeds.Buildings());
			}

			WorldBuildingCatalog fromAsset = new WorldBuildingCatalog(JsonUtility.FromJson<BuildingCatalogData>(asset.text));
			return fromAsset.Count > 0 ? fromAsset : new WorldBuildingCatalog(WorldSeeds.Buildings());
		}
	}

	/// <summary>
	/// 내 안의 세계가 든 마도서 (TASK-WM-217) — 완성이 무엇을 주는지의 정본.
	/// 아직 뽑는 도구가 없으므로 씨앗으로 돈다(서버와 <b>같은</b> 씨앗이라 갈라지지 않는다).
	/// </summary>
	/// <summary>내 안의 세계가 든 제작표 — 씨앗으로 돈다(진짜 자산은 뽑아서 꽂는다).</summary>
	public static class CraftBookOf
	{
		private static WorldCraftBook book;

		public static WorldCraftBook Loaded => book ?? (book = new WorldCraftBook(WorldSeeds.Crafts()));
	}

	public static class RecipeBook
	{
		private static WorldRecipeBook book;

		public static WorldRecipeBook Loaded => book ?? (book = new WorldRecipeBook(WorldSeeds.Recipes()));
	}
}
