using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(ItemData), menuName = "WM/Variable/ItemData")]
	public class ItemData : DataSO, IItemData
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

		/// <summary>검증·에디터 도구가 값을 물린다 (WitchPlantSO.EditorSet* 선례).</summary>
		public void EditorSetPrices(int purchasePrice, int salePrice)
		{
			PurchasePrice = purchasePrice;
			SalePrice = salePrice;
		}
	}
}
