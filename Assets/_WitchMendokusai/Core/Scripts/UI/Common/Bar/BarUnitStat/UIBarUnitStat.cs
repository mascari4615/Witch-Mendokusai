using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIBarUnitStat : UIBarStat<UnitStatType>
	{
		protected override Stat<UnitStatType> GetStat()
		{
			return Player.Instance.UnitStat;
		}
	}
}