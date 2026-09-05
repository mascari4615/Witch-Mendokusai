using System;
using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	/// <summary>솥에 넣을 수 있는 재료 하나 — 어느 쪽으로 얼마나 미나 (TASK-WM-217).</summary>
	[Serializable]
	public class IngredientCatalogEntry
	{
		public int itemId;
		public string name = string.Empty;
		public float dx;
		public float dy;
		public float grind = 1f;
	}

	/// <summary>세계가 아는 재료들 (아이템·건물 목록과 같은 모양).</summary>
	[Serializable]
	public class IngredientCatalogData
	{
		public IngredientCatalogEntry[] ingredients = Array.Empty<IngredientCatalogEntry>();
	}

	/// <summary>
	/// 「무엇을 넣으면 어디로 가나」를 <b>세계가 안다</b> (TASK-WM-217).
	///
	/// ★ 왜: 전에는 창이 「이 방향으로 이만큼 저었다」고 보냈다. 그러면 아무것도 안 들고도 저을 수 있고,
	///   창을 고친 사람은 한 번에 목표 한가운데로 갈 수 있다. 이제 창은 <b>무엇을 넣는지만</b> 말하고,
	///   방향·세기는 세계가 재료에서 읽는다 — 그래서 <b>줍기가 조리의 재료가 된다</b>(루프가 닫힌다).
	/// </summary>
	public sealed class WorldIngredients
	{
		private readonly Dictionary<int, IngredientCatalogEntry> byItem = new Dictionary<int, IngredientCatalogEntry>();
		private readonly List<IngredientCatalogEntry> ordered = new List<IngredientCatalogEntry>();

		public WorldIngredients(IngredientCatalogData data)
		{
			if (data?.ingredients == null)
				return;

			for (int i = 0; i < data.ingredients.Length; i++)
			{
				IngredientCatalogEntry entry = data.ingredients[i];
				if (entry == null || byItem.ContainsKey(entry.itemId))
					continue;

				byItem[entry.itemId] = entry;
				ordered.Add(entry);
			}
		}

		/// <summary>아는 재료 수 — 0 이면 아무것도 못 넣는다(솥이 안 돈다).</summary>
		public int Count => ordered.Count;

		/// <summary>창이 「무엇을 넣을 수 있나」 보여 주려면 필요하다.</summary>
		public IReadOnlyList<IngredientCatalogEntry> All => ordered;

		/// <summary>그 아이템이 재료인가 — 맞으면 어떻게 젓는 한 걸음인지 알려 준다.</summary>
		public bool TryStep(int itemId, out BrewStep step)
		{
			step = default;
			if (byItem.TryGetValue(itemId, out IngredientCatalogEntry entry) == false)
				return false;

			step = new BrewStep
			{
				Direction = new BrewVector(entry.dx, entry.dy),
				Grind = entry.grind <= 0f ? 1f : entry.grind,
			};
			return true;
		}
	}
}
