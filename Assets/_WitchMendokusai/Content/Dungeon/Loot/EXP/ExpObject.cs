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

		private UnitStat PlayerStat => Player.Instance.UnitStat;

		public override void Effect()
		{
			RuntimeManager.PlayOneShot("event:/SFX/EXP", transform.position);
			PlayerStat[UnitStatType.EXP_CUR] += amount;
		}
	}
}