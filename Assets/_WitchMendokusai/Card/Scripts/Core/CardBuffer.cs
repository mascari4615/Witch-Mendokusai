using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(CardBuffer), menuName = "DataBuffer/" + nameof(CardData))]
	public class CardBuffer : DataBufferSO<CardData>
	{
		[SerializeField] private bool applyEffect;

		public override void Add(CardData card)
		{
			base.Add(card);
			if (applyEffect)
				card.OnEquip();
		}

		public override bool Remove(CardData card)
		{
			if (applyEffect)
				if (Datas.Contains(card))
					card.OnRemove();

			return base.Remove(card);
		}
	}
}