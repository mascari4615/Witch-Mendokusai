using System;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class ExpObject : LootObject
	{
		[SerializeField] private int amount;
		private int rewardMultiplier = 1;

		public void SetRewardMultiplier(int value)
		{
			rewardMultiplier = Mathf.Max(1, value);
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		protected override void OnEffect()
		{
			RuntimeManager.PlayOneShot("event:/SFX/EXP", transform.position);
			PlayerStat[UnitStatType.EXP_CUR] += amount * rewardMultiplier;
		}
	}
}
