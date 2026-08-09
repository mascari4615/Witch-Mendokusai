using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>아이템 한 종류의 <b>세계가 알아야 할 최소</b> (TASK-WM-217).</summary>
	[Serializable]
	public class ItemCatalogEntry
	{
		public int id;
		public string name = string.Empty;
		public int maxAmount = 1;
		public int type;
		public int grade;
	}

	/// <summary>
	/// 세계가 아는 아이템 목록 (TASK-WM-217).
	///
	/// ★ 왜 필요한가: 지금 서버는 아이템을 <b>손으로 적은 씨앗 두 개</b>로 안다. 그러면 게임에서
	///   아이템을 하나 추가할 때마다 서버 코드를 같이 고쳐야 하고, 반드시 갈라진다
	///   (「손으로 적은 낱말표는 반드시 샌다」). 정본은 게임 자산이고, 여기는 그것을 <b>뽑아 담는 그릇</b>이다.
	///
	/// 필드가 public 인 이유 = 유니티 JsonUtility 와 서버 System.Text.Json 이 둘 다 이 모양만 읽는다.
	/// </summary>
	[Serializable]
	public class ItemCatalogData
	{
		public ItemCatalogEntry[] items = Array.Empty<ItemCatalogEntry>();
	}

	/// <summary>목록 하나를 <see cref="IItemData"/> 로 꺼내 쓰는 자리 — 가방 규칙이 그 계약만 안다.</summary>
	public sealed class WorldItemCatalog
	{
		private sealed class Entry : IItemData
		{
			public Entry(ItemCatalogEntry source)
			{
				ID = source.id;
				// 이름이 비면 번호로 부른다 — 창에 「(빈칸) 3개」가 뜨는 것보다 낫다.
				Name = string.IsNullOrWhiteSpace(source.name) ? "#" + source.id : source.name;
				MaxAmount = source.maxAmount < 1 ? 1 : source.maxAmount;
				Type = (ItemType)source.type;
				Grade = (ItemGrade)source.grade;
			}

			public int ID { get; }
			public string Name { get; }
			public int MaxAmount { get; }
			public ItemType Type { get; }
			public ItemGrade Grade { get; }
		}

		private readonly Dictionary<int, Entry> byId = new Dictionary<int, Entry>();

		public WorldItemCatalog(ItemCatalogData data)
		{
			if (data?.items == null)
				return;

			for (int i = 0; i < data.items.Length; i++)
			{
				ItemCatalogEntry entry = data.items[i];
				if (entry == null)
					continue;

				// 같은 번호가 두 번 오면 <b>먼저 것</b>을 남긴다 — 조용히 바뀌는 것보다 낫다.
				if (byId.ContainsKey(entry.id))
					continue;

				byId[entry.id] = new Entry(entry);
			}
		}

		/// <summary>아는 아이템 수 — 0개면 「비었다」가 아니라 「못 읽었다」일 수 있다.</summary>
		public int Count => byId.Count;

		/// <summary>그 번호의 아이템. 모르면 null(세계는 모르는 것을 가방에 넣지 않는다).</summary>
		public IItemData Find(int itemId) => byId.TryGetValue(itemId, out Entry entry) ? entry : null;

		/// <summary>
		/// 사람에게 보일 이름 (TASK-WM-217 — 창이 「17450 3개」가 아니라 「돌 3개」를 보이려면 필요하다).
		/// 모르는 번호면 <c>#번호</c> — 빈칸을 보이면 「없는 것」처럼 읽힌다.
		/// </summary>
		public string NameOf(int itemId) => byId.TryGetValue(itemId, out Entry entry) ? entry.Name : "#" + itemId;

		/// <summary>아는 것 전부 — 창에 한 번 내려보내는 낱말표를 만들 때 쓴다.</summary>
		public IEnumerable<KeyValuePair<int, string>> Names()
		{
			foreach (KeyValuePair<int, Entry> pair in byId)
				yield return new KeyValuePair<int, string>(pair.Key, pair.Value.Name);
		}
	}
}
