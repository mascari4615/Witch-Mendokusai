namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class ItemEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			Inventory inventory = context.SOManager.ItemInventory;
			ItemData targetItem = effectInfo.Data as ItemData;
			int amount = effectInfo.Value;

			if (effectInfo.ArithmeticOperator == ArithmeticOperator.Add)
				inventory.Add(targetItem, amount);
			else if (effectInfo.ArithmeticOperator == ArithmeticOperator.Subtract)
				inventory.Remove(inventory.FindItemIndex(targetItem), amount);
		}
	}
}
