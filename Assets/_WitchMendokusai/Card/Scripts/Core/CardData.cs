using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "C_", menuName = "Variable/" + nameof(CardData))]
	public class CardData : DataSO
	{
		[field: Header("_" + nameof(CardData))]
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; }
		[field: SerializeField] public int MaxStack { get; private set; } = 5;

		public void OnEquip()
		{
			Effect.ApplyEffects(Effects);
		}

		public void OnRemove()
		{
		}
	}
}