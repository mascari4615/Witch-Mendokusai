using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 게임의 아이템 정의를 <b>세계가 읽을 수 있는 목록</b>으로 뽑는다 (TASK-WM-217).
	///
	/// ★ 왜: 서버는 유니티 에셋을 못 읽는다. 그렇다고 서버에 손으로 적으면 반드시 갈라진다
	///   (게임에 아이템 하나 추가할 때마다 서버도 고쳐야 한다). <b>정본은 게임 자산</b>이고,
	///   이 도구가 그걸 한 벌로 뽑는다 — 서버·웹·내 안의 세계가 같은 파일을 본다.
	///
	/// 넣는 곳 두 군데: 서버 옆(<c>Server/WM.Server/items.json</c>)과 게임 안
	/// (<c>Resources/items.json</c> — 인터넷 없이 혼자 놀 때 쓴다).
	/// </summary>
	public static class ItemCatalogExporter
	{
		private const string SERVER_RELATIVE = "Server/WM.Server/items.json";
		private const string RESOURCES_PATH = "Assets/_WitchMendokusai/Resources/items.json";
		private const string BUILDINGS_SERVER_RELATIVE = "Server/WM.Server/buildings.json";
		private const string BUILDINGS_RESOURCES_PATH = "Assets/_WitchMendokusai/Resources/buildings.json";
		private const string CRAFTS_SERVER_RELATIVE = "Server/WM.Server/crafts.json";
		private const string CRAFTS_RESOURCES_PATH = "Assets/_WitchMendokusai/Resources/crafts.json";

		[MenuItem("WM/Export Item Catalog (for World)")]
		public static void Export()
		{
			List<ItemCatalogEntry> entries = new List<ItemCatalogEntry>();
			string[] guids = AssetDatabase.FindAssets("t:ItemData");

			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(path);
				if (data == null)
					continue;

				entries.Add(new ItemCatalogEntry
				{
					id = data.ID,
					// 이름까지 뽑는 이유: 창(웹)은 유니티 에셋을 못 읽는다 — 이름이 없으면 「17450 3개」로 보인다.
					name = data.Name,
					maxAmount = data.MaxAmount,
					type = (int)data.Type,
					grade = (int)data.Grade,
				});
			}

			// 0개면 「비었다」가 아니라 「못 찾았다」다 — 그 상태로 덮어쓰면 세계가 아이템을 잊는다.
			if (entries.Count == 0)
			{
				Debug.LogError("[items] ItemData 를 하나도 못 찾았다 — 덮어쓰지 않는다(찾는 방식을 확인할 것).");
				return;
			}

			ItemCatalogData catalog = new ItemCatalogData { items = entries.ToArray() };
			string json = JsonUtility.ToJson(catalog, true);

			string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
			WriteIfChanged(Path.Combine(projectRoot, "WitchMendokusai", SERVER_RELATIVE), json);
			WriteIfChanged(RESOURCES_PATH, json);

			ExportBuildings();
			ExportCrafts();

			AssetDatabase.Refresh();
			Debug.Log($"[items] 아이템 {entries.Count}종을 뽑았다.");
		}

		/// <summary>
		/// 제작표도 같이 뽑는다 (TASK-WM-217) — <b>재료도 성공률도 세계가 판정</b>하려면 세계가 알아야 한다.
		///
		/// ★ 줄의 번호 = <b>결과 아이템 번호</b>. 따로 매기면 뽑을 때마다 번호가 흔들려
		///   어제 되던 제작이 오늘 안 된다. 게임 화면도 「이 아이템을 만들겠다」로 고른다.
		/// </summary>
		private static void ExportCrafts()
		{
			List<CraftRecipeEntry> entries = new List<CraftRecipeEntry>();
			string[] guids = AssetDatabase.FindAssets("t:ItemData");

			for (int i = 0; i < guids.Length; i++)
			{
				ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guids[i]));
				if (data == null || data.Recipes == null || data.Recipes.Count == 0)
					continue;

				Recipe recipe = data.Recipes[0];
				if (recipe.Items == null || recipe.Items.Count == 0)
					continue; // 재료가 없는 줄은 「공짜로 무엇이든」이 된다 — 세계에 안 올린다.

				List<CraftIngredientEntry> items = new List<CraftIngredientEntry>();
				bool broken = false;
				foreach (ItemInfo need in recipe.Items)
				{
					if (need.ItemData == null)
					{
						broken = true; // 재료 한 칸이 비면 그 줄은 공짜가 된다 — 통째로 버린다.
						break;
					}

					items.Add(new CraftIngredientEntry { itemId = need.ItemData.ID, amount = need.Amount <= 0 ? 1 : need.Amount });
				}

				if (broken)
					continue;

				entries.Add(new CraftRecipeEntry
				{
					id = data.ID,
					name = data.Name,
					resultItemId = data.ID,
					resultAmount = recipe.Amount <= 0 ? 1 : recipe.Amount,
					percentage = recipe.Percentage <= 0f ? 100f : recipe.Percentage,
					items = items.ToArray(),
				});
			}

			if (entries.Count == 0)
			{
				Debug.LogWarning("[crafts] 재료가 적힌 제작 줄을 하나도 못 찾았다 — 덮어쓰지 않는다(씨앗으로 돈다).");
				return;
			}

			string json = JsonUtility.ToJson(new CraftCatalogData { recipes = entries.ToArray() }, true);
			string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
			WriteIfChanged(Path.Combine(projectRoot, "WitchMendokusai", CRAFTS_SERVER_RELATIVE), json);
			WriteIfChanged(CRAFTS_RESOURCES_PATH, json);

			Debug.Log($"[crafts] 제작 {entries.Count}줄을 뽑았다.");
		}

		/// <summary>
		/// 지을 수 있는 것들도 같이 뽑는다 (TASK-WM-217) — <b>크기는 세계가 알아야 한다</b>.
		/// 안 뽑으면 세계는 아무것도 못 짓는다(모르는 것은 서지 않는다).
		/// </summary>
		private static void ExportBuildings()
		{
			List<BuildingCatalogEntry> entries = new List<BuildingCatalogEntry>();
			string[] guids = AssetDatabase.FindAssets("t:Building");

			for (int i = 0; i < guids.Length; i++)
			{
				Building data = AssetDatabase.LoadAssetAtPath<Building>(AssetDatabase.GUIDToAssetPath(guids[i]));
				if (data == null)
					continue;

				entries.Add(new BuildingCatalogEntry
				{
					id = data.ID,
					name = data.Name,
					w = data.Size.x < 1 ? 1 : data.Size.x,
					l = data.Size.y < 1 ? 1 : data.Size.y,
					slots = data.StorageSlots,
					// 재료 = 씨앗이 정한 것(건물마다 다르다). 자산에 재료 칸이 생기면 그쪽이 이긴다.
					// 공짜로 지어지면 줍기가 뜻을 잃고, 전부 나무면 조리가 막다른 길이 된다.
					costItemId = WorldSeeds.CostItemOf(data.ID),
					costAmount = data.Cost > 0 ? data.Cost : 2,
				});
			}

			if (entries.Count == 0)
			{
				Debug.LogError("[buildings] 지을 것을 하나도 못 찾았다 — 덮어쓰지 않는다.");
				return;
			}

			string json = JsonUtility.ToJson(new BuildingCatalogData { buildings = entries.ToArray() }, true);
			string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
			WriteIfChanged(Path.Combine(projectRoot, "WitchMendokusai", BUILDINGS_SERVER_RELATIVE), json);
			WriteIfChanged(BUILDINGS_RESOURCES_PATH, json);
			Debug.Log($"[buildings] 지을 것 {entries.Count}종을 뽑았다.");
		}

		private static void WriteIfChanged(string path, string json)
		{
			string directory = Path.GetDirectoryName(path);
			if (string.IsNullOrEmpty(directory) == false)
				Directory.CreateDirectory(directory);

			// 안 바뀌었으면 안 쓴다 — 매번 쓰면 git 이 매번 바뀐 것으로 본다.
			if (File.Exists(path) && File.ReadAllText(path) == json)
				return;

			File.WriteAllText(path, json);
		}
	}
}
