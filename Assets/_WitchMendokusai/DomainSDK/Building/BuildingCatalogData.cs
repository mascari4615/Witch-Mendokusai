using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>지을 수 있는 것 하나 — 세계가 알아야 할 최소 (TASK-WM-217).</summary>
	[Serializable]
	public class BuildingCatalogEntry
	{
		public int id;
		public string name = string.Empty;
		public int w = 1;
		public int l = 1;

		/// <summary>물건을 넣어 둘 수 있는 칸 수 (0 = 상자가 아니다).</summary>
		public int slots;

		/// <summary>지을 때 드는 재료 — 무엇을 몇 개. amount 0 = 공짜.</summary>
		public int costItemId;
		public int costAmount;
	}

	/// <summary>세계가 아는 건물 목록 (아이템 목록과 같은 모양 — 정본은 게임 자산).</summary>
	[Serializable]
	public class BuildingCatalogData
	{
		public BuildingCatalogEntry[] buildings = Array.Empty<BuildingCatalogEntry>();
	}

	/// <summary>
	/// 「그건 몇 칸짜리인가」를 <b>세계가 안다</b> (TASK-WM-217).
	///
	/// ★ 왜 세계가 알아야 하나: 전에는 창이 「이거 3×3 이다」라고 같이 보냈고 세계는 그대로 믿었다.
	///   그러면 창을 고친 사람이 1×1 이라고 우기며 남의 집 옆에 겹쳐 지을 수 있다 —
	///   그리고 게임 창과 웹 창이 서로 다른 크기로 그린다(같은 세계가 아니게 된다).
	/// </summary>
	public sealed class WorldBuildingCatalog
	{
		private readonly Dictionary<int, BuildingCatalogEntry> byId = new Dictionary<int, BuildingCatalogEntry>();
		private readonly List<BuildingCatalogEntry> ordered = new List<BuildingCatalogEntry>();

		public WorldBuildingCatalog(BuildingCatalogData data)
		{
			if (data?.buildings == null)
				return;

			for (int i = 0; i < data.buildings.Length; i++)
			{
				BuildingCatalogEntry entry = data.buildings[i];
				if (entry == null || byId.ContainsKey(entry.id))
					continue;

				byId[entry.id] = entry;
				ordered.Add(entry);
			}
		}

		/// <summary>아는 것 수 — 0 이면 아무것도 못 짓는다(세계가 모르는 것은 안 선다).</summary>
		public int Count => ordered.Count;

		/// <summary>목록 그대로 — 창이 「무엇을 지을까」 고르게 하려면 필요하다.</summary>
		public IReadOnlyList<BuildingCatalogEntry> All => ordered;

		/// <summary>모르는 번호면 null — 세계가 모르는 것은 서지 않는다.</summary>
		public BuildingCatalogEntry Find(int buildingId)
		{
			return byId.TryGetValue(buildingId, out BuildingCatalogEntry entry) ? entry : null;
		}

		/// <summary>그것의 크기. 모르면 (0,0) — 부르는 쪽이 「모른다」를 구분할 수 있게.</summary>
		public bool TrySize(int buildingId, out int width, out int length)
		{
			width = 0;
			length = 0;
			if (byId.TryGetValue(buildingId, out BuildingCatalogEntry entry) == false)
				return false;

			width = entry.w < 1 ? 1 : entry.w;
			length = entry.l < 1 ? 1 : entry.l;
			return true;
		}

		/// <summary>이걸 지으려면 무엇이 몇 개 드나 — amount 0 이면 공짜다.</summary>
		public bool TryCost(int buildingId, out int itemId, out int amount)
		{
			itemId = 0;
			amount = 0;
			if (byId.TryGetValue(buildingId, out BuildingCatalogEntry entry) == false)
				return false;

			itemId = entry.costItemId;
			amount = entry.costAmount < 0 ? 0 : entry.costAmount;
			return true;
		}

		/// <summary>이건 몇 칸짜리 상자인가 — 0 이면 상자가 아니다.</summary>
		public int SlotsOf(int buildingId)
		{
			return byId.TryGetValue(buildingId, out BuildingCatalogEntry entry) ? entry.slots : 0;
		}

		/// <summary>사람에게 보일 이름 — 모르면 <c>#번호</c>.</summary>
		public string NameOf(int buildingId)
		{
			return byId.TryGetValue(buildingId, out BuildingCatalogEntry entry) && string.IsNullOrWhiteSpace(entry.name) == false
				? entry.name
				: "#" + buildingId;
		}
	}
}
