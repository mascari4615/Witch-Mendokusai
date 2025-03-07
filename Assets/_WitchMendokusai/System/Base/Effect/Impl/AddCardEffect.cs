using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class AddCardEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			CardData targetCard = effectInfo.Data as CardData;
			SOManager.Instance.SelectedCardBuffer.Add(targetCard);
		}
	}
}