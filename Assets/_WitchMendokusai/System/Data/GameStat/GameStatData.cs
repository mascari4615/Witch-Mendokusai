using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "GSD_", menuName = "Variable/" + nameof(GameStatData))]
	public class GameStatData : StatData<GameStatType>
	{
	}
}