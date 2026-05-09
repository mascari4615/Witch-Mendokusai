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
				SOManager.Instance.ItemInventory.Add(targetItem, amount);
			else if (effectInfo.ArithmeticOperator == ArithmeticOperator.Subtract)
				SOManager.Instance.ItemInventory.Remove(SOManager.Instance.ItemInventory.FindItemIndex(targetItem), amount);
		}
	}
}