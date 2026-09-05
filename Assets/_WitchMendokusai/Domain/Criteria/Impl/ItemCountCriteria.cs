namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2C-4 — Bridge 의존 완전 폐기. ctx 는 QuestManager.CreateCriteriaContext() 단일 지점에서
	// 모든 호출처가 공급 (null 시 NRE = FastFail, 방어 fallback X — WM FastFail 룰).
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
			Inventory inventory = context.SOManager.ItemInventory;
			return inventory.GetItemAmount(ItemID);
		}
	}
}
