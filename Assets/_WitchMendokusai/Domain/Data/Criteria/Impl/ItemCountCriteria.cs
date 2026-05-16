namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C — ctx 경로 = ctx.SOManager (DI caller thread).
	// ctx null = SOManagerBridge transitional fallback (미thread 호출처 — 후속 수렴 시 제거).
	public class ItemCountCriteria : NumCriteria
	{
		public int ItemID { get; private set; } = DataSO.NONE_ID;

		private readonly CriteriaContext context;

		public ItemCountCriteria(CriteriaInfo criteriaInfo, CriteriaContext context = null) : base(criteriaInfo)
		{
			ItemID = criteriaInfo.Data.ID;
			this.context = context;
		}

		public override int GetCurValue()
		{
			Inventory inventory = context == null ? SOManagerBridge.ItemInventory : context.SOManager.ItemInventory;
			return inventory.GetItemAmount(ItemID);
		}
	}
}
