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

		[MenuItem("WM/아이템 목록 뽑기 (세계용)")]
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

			AssetDatabase.Refresh();
			Debug.Log($"[items] 아이템 {entries.Count}종을 뽑았다.");
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
