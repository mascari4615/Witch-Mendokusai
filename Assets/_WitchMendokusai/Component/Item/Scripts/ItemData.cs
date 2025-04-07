using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public enum ItemGrade
	{
		Common,
		Uncommon,
		Rare,
		Legendary
	}

	public enum ItemType
	{
		None = -1,
		Loot = 0,
		Potion = 1,
		Equipment = 2,
		Aspects = 3,
		Count,
	}

	[CreateAssetMenu(fileName = nameof(ItemData), menuName = "Variable/ItemData")]
	public class ItemData : DataSO
	{
		[field: Header("_" + nameof(ItemData))]
		[PropertyOrder(10)][field: SerializeField] public ItemGrade Grade { get; private set; } = new ();
		[PropertyOrder(11)][field: SerializeField] public ItemType Type { get; private set; } = new ();
		[PropertyOrder(12)][field: SerializeField] public List<Recipe> Recipes { get; private set; } = new ();
		[PropertyOrder(13)][field: SerializeField] public int MaxAmount { get; private set; } = 500;
		[PropertyOrder(14)][field: SerializeField] public int PurchasePrice { get; private set; } = 0;
		[PropertyOrder(15)][field: SerializeField] public int SalePrice { get; private set; } = 0;

		public Item CreateItem()
		{
			return new Item(Guid.NewGuid(), this);
		}

		public bool IsCountable => MaxAmount != 1;
		public bool Unlocked => DataManager.Instance.IsRecipeUnlocked.TryGetValue(ID, out bool unlocked) && unlocked;
	}
}