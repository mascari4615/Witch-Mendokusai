using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class ItemEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			ItemData targetItem = effectInfo.Data as ItemData;
			int amount = effectInfo.Value;

			if (effectInfo.ArithmeticOperator == ArithmeticOperator.Add)
				SOManagerBridge.ItemInventory.Add(targetItem, amount);
			else if (effectInfo.ArithmeticOperator == ArithmeticOperator.Subtract)
				SOManagerBridge.ItemInventory.Remove(SOManagerBridge.ItemInventory.FindItemIndex(targetItem), amount);
		}
	}
}