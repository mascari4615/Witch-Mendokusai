using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class StageStrategy
	{
		public abstract void Init(Stage stage);
	}
}