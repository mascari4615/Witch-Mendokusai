using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class DungeonStat : Stat<DungeonStatType>
	{
		public DungeonStat() : base()
		{
		}

		protected override void InitValue()
		{
			foreach (DungeonStatType statType in Enum.GetValues(typeof(DungeonStatType)))
			{
				int value = statType switch
				{
					_ => 0,
				};

				stats[statType] = value;
			}
		}
	}
}