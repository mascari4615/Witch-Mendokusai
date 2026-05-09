using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발용 치트 콘솔. F2로 토글.
	/// 아이템 지급 / 몬스터 스폰 / 던전 이동 / 퀘스트 언락
	/// </summary>
	public class WMDevConsole : MonoBehaviour
	{
		private bool show = false;
		private Vector2 scroll = Vector2.zero;

		// 아이템
		private string itemSearchText = "";
		private int itemAmount = 1;

		// 몬스터
		private string monsterSearchText = "";

		// 던전
		private string dungeonSearchText = "";

		// 퀘스트
		private string questSearchText = "";

		private InputAction actionToggle;

		private void OnEnable()
		{
			actionToggle ??= new InputAction("DevConsoleToggle", InputActionType.Button);
			actionToggle.AddBinding("<Keyboard>/f2");
			actionToggle.Enable();
		}

		private void OnDisable()
		{
			actionToggle?.Disable();
		}

		private void Update()
		{
			if (actionToggle.WasPressedThisFrame())
				show = !show;
		}

		private void OnGUI()
		{
			GUIStyle toggleStyle = new GUIStyle(GUI.skin.button)
			{
				fontSize = 15,
				fontStyle = FontStyle.Bold,
				fixedHeight = 28f,
				fixedWidth = 100f,
			};
			toggleStyle.normal.textColor = show ? new Color(1f, 0.86f, 0.45f) : new Color(0.6f, 1f, 0.6f);

			if (GUI.Button(new Rect(Screen.width - 112f, 12f, 100f, 28f), show ? "▼ DEV" : "▶ DEV", toggleStyle))
				show = !show;

			if (!show) return;

			const float W = 520f;
			const float H = 700f;
			float x = Screen.width - W - 12f;
			float y = 48f;

			GUIStyle box = new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, padding = new RectOffset(12, 12, 12, 12), alignment = TextAnchor.UpperLeft };
			GUIStyle header = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
			header.normal.textColor = new Color(1f, 0.86f, 0.45f);
			GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, fixedHeight = 36f };
			GUIStyle field = new GUIStyle(GUI.skin.textField) { fontSize = 15, fixedHeight = 30f };

			GUI.Box(new Rect(x, y, W, H), "WM DEV CONSOLE  (F2)", box);

			Rect scrollRect = new Rect(x + 10f, y + 44f, W - 20f, H - 56f);
			Rect content = new Rect(0f, 0f, scrollRect.width - 20f, 1200f);
			scroll = GUI.BeginScrollView(scrollRect, scroll, content);

			float cx = 4f;
			float cy = 8f;
			float cw = content.width - 8f;

			// ── 아이템 지급 ──────────────────────────────
			GUI.Label(new Rect(cx, cy, cw, 24f), "■ 아이템 지급", header);
			cy += 28f;
			itemSearchText = GUI.TextField(new Rect(cx, cy, cw - 90f, 30f), itemSearchText, field);
			GUI.Label(new Rect(cx + cw - 86f, cy + 4f, 30f, 22f), "x");
			itemAmount = int.TryParse(GUI.TextField(new Rect(cx + cw - 52f, cy, 52f, 30f), itemAmount.ToString(), field), out int a) ? Mathf.Max(1, a) : itemAmount;
			cy += 36f;

			DrawFilteredList<ItemData>(cx, ref cy, cw, itemSearchText, item => item.Name,
				item =>
				{
					SOManager.Instance.ItemInventory.Add(item, itemAmount);
					Debug.Log($"[DEV] 아이템 지급: {item.Name} x{itemAmount}");
				}, btnLabel: item => $"{item.Name}  (ID {item.ID})");

			cy += 12f;

			// ── 몬스터 스폰 ──────────────────────────────
			GUI.Label(new Rect(cx, cy, cw, 24f), "■ 몬스터 스폰", header);
			cy += 28f;
			monsterSearchText = GUI.TextField(new Rect(cx, cy, cw, 30f), monsterSearchText, field);
			cy += 36f;

			DrawFilteredList<Monster>(cx, ref cy, cw, monsterSearchText, m => m.Name,
				m => SpawnMonsterNearPlayer(m),
				btnLabel: m => $"{m.Name}  (ID {m.ID})");

			cy += 12f;

			// ── 던전 이동 ──────────────────────────────
			GUI.Label(new Rect(cx, cy, cw, 24f), "■ 던전 이동", header);
			cy += 28f;
			dungeonSearchText = GUI.TextField(new Rect(cx, cy, cw, 30f), dungeonSearchText, field);
			cy += 36f;

			DrawFilteredList<Dungeon>(cx, ref cy, cw, dungeonSearchText, d => d.Name,
				d => DungeonManager.Instance.StartDungeon(d),
				btnLabel: d => $"{d.Name}  (ID {d.ID})");

			cy += 12f;

			// ── 퀘스트 언락 ──────────────────────────────
			GUI.Label(new Rect(cx, cy, cw, 24f), "■ 퀘스트 언락", header);
			cy += 28f;
			questSearchText = GUI.TextField(new Rect(cx, cy, cw, 30f), questSearchText, field);
			cy += 36f;

			DrawFilteredList<QuestSO>(cx, ref cy, cw, questSearchText, q => q.Name,
				q =>
				{
					QuestManager.Instance.UnlockQuest(q);
					Debug.Log($"[DEV] 퀘스트 언락: {q.Name} (ID {q.ID})");
				},
				btnLabel: q => $"{q.Name}  (ID {q.ID})");

			GUI.EndScrollView();
		}

		private void DrawFilteredList<T>(float x, ref float y, float w, string search, System.Func<T, string> getName, System.Action<T> onPress, System.Func<T, string> btnLabel = null) where T : DataSO
		{
			GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 34f, alignment = TextAnchor.MiddleLeft };
			btn.padding = new RectOffset(10, 6, 0, 0);

			List<T> filtered = new();
			ForEach<T>(item =>
			{
				if (filtered.Count >= 20) return;
				string name = getName(item);
				if (!string.IsNullOrEmpty(search) && !name.Contains(search, System.StringComparison.OrdinalIgnoreCase)) return;
				filtered.Add(item);
			});

			if (filtered.Count == 0)
			{
				GUIStyle none = new GUIStyle(GUI.skin.label) { fontSize = 13 };
				none.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
				GUI.Label(new Rect(x, y, w, 24f), "(검색 결과 없음)", none);
				y += 28f;
				return;
			}

			foreach (T item in filtered)
			{
				string label = btnLabel != null ? btnLabel(item) : getName(item);
				if (GUI.Button(new Rect(x, y, w, 34f), label, btn))
					onPress(item);
				y += 38f;
			}
		}

		private void SpawnMonsterNearPlayer(Monster monster)
		{
			Vector3 pos = PlayerProvider.Instance.Current.transform.position;
			Vector2 rand = Random.insideUnitCircle.normalized;
			pos += new Vector3(rand.x, 0f, rand.y) * 5f;

			GameObject obj = ObjectPoolManager.Instance.Spawn(monster.Prefab);
			obj.transform.position = pos;
			obj.GetComponent<MonsterObject>().Init(monster);
			obj.SetActive(true);
			Debug.Log($"[DEV] 몬스터 스폰: {monster.Name}");
		}
	}
}
