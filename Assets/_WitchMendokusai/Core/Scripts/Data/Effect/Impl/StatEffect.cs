using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class StatEffect : IEffect
	{
		private UnitStat PlayerStat => Player.Instance.UnitStat;

		public void Apply(EffectInfo effectInfo)
		{
			UnitStatType Type = (effectInfo.Data as UnitStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(PlayerStat[Type], value, arithmeticOperator);
			PlayerStat[Type] = newValue;
		}
	}
}