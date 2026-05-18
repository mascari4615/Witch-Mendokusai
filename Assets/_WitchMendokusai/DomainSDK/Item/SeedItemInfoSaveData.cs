using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct SeedItemInfoSaveData
	{
		public int ID;
		public string Name;
		public string Description;
		public ItemGrade Grade;
		public ItemType Type;
		public int MaxAmount;
		public int PurchasePrice;
		public int SalePrice;
		public float GrowSeconds;
	}
}
