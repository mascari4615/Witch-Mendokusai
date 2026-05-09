namespace WitchMendokusai
{
	public class ItemCountCriteria : NumCriteria
	{
		public int ItemID { get; private set; } = DataSO.NONE_ID;

		public ItemCountCriteria(CriteriaInfo criteriaInfo) : base(criteriaInfo)
		{
			ItemID = criteriaInfo.Data.ID;
		}

		public override int GetCurValue()
		{
			Inventory inventory = SOManager.Instance.ItemInventory;
			return inventory.GetItemAmount(ItemID);
		}
	}
}