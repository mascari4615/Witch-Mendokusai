using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIBarGameStat : UIBarStat<GameStatType>
	{
		protected override Stat<GameStatType> GetStat()
		{
			return DataManager.Instance.GameStat;
		}
	}
}