using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "GSD_", menuName = "WM/Variable/" + nameof(GameStatData))]
	public class GameStatData : StatData<GameStatType>
	{
	}
}