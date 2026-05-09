using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public enum StageDungeonStep
	{
		None,
		Enter,
		Explore,
		Battle,
		Exit,
	}

	public class StageStrategyDungeon : StageStrategy
	{
		public override void Init(Stage stage)
		{

		}
	}
}