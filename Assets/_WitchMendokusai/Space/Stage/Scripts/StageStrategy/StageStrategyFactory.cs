using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public static class StageStrategyFactory
	{
		public static StageStrategy Create(Stage stage)
		{
			return stage.Type switch
			{
				StageType.Dungeon => new StageStrategyDungeon(),
				StageType.World => new StageStrategyWorld(),
				_ => throw new ArgumentOutOfRangeException(nameof(stage.Type), stage.Type, null)
			};
		}
	}
}