namespace WitchMendokusai
{
	public static class ItemDataExtensions
	{
		public static ItemInfoSaveData ToSaveData(this ItemData itemData)
		{
			if (itemData == null)
			{
				return default;
			}

			return new ItemInfoSaveData
			{
				ID = itemData.ID,
				Name = itemData.Name,
				Description = itemData.Description,
				Grade = itemData.Grade,
				Type = itemData.Type,
				MaxAmount = itemData.MaxAmount,
				PurchasePrice = itemData.PurchasePrice,
				SalePrice = itemData.SalePrice
			};
		}

		public static SeedItemInfoSaveData ToSaveData(this SeedItemData seedItemData)
		{
			if (seedItemData == null)
			{
				return default;
			}

			return new SeedItemInfoSaveData
			{
				ID = seedItemData.ID,
				Name = seedItemData.Name,
				Description = seedItemData.Description,
				Grade = seedItemData.Grade,
				Type = seedItemData.Type,
				MaxAmount = seedItemData.MaxAmount,
				PurchasePrice = seedItemData.PurchasePrice,
				SalePrice = seedItemData.SalePrice,
				GrowSeconds = seedItemData.GrowSeconds
			};
		}
	}
}
