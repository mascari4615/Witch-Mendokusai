namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2B — IContextualEffect 경로 = ctx.SOManager (DI runner).
	// 구 IEffect 경로 = SOManagerBridge transitional (정적 SO 호출처 CardData/UpgradeData 용 — 후속 수렴 시 제거).
	public class ItemEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo) => Apply(effectInfo, SOManagerBridge.ItemInventory);

		public void Apply(EffectInfo effectInfo, EffectContext context) => Apply(effectInfo, context.SOManager.ItemInventory);

		private static void Apply(EffectInfo effectInfo, Inventory inventory)
		{
			ItemData targetItem = effectInfo.Data as ItemData;
			int amount = effectInfo.Value;

			if (effectInfo.ArithmeticOperator == ArithmeticOperator.Add)
				inventory.Add(targetItem, amount);
			else if (effectInfo.ArithmeticOperator == ArithmeticOperator.Subtract)
				inventory.Remove(inventory.FindItemIndex(targetItem), amount);
		}
	}
}
