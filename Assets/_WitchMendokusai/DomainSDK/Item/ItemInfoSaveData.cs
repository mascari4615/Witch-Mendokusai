using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct ItemInfoSaveData
	{
		public int ID;
		public string Name;
		public string Description;
		public ItemGrade Grade;
		public ItemType Type;
		public int MaxAmount;
		public int PurchasePrice;
		public int SalePrice;
	}
}
