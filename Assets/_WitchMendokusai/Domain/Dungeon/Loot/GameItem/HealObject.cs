using System;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class HealObject : GameItemObject
	{
		[SerializeField] private int healAmount;

		protected override void OnEffect()
		{
			CombatSfx.Play(CombatSfxEvent.Heal);
			playerProvider.CurrentObject.ReceiveHeal(healAmount);
		}
	}
}