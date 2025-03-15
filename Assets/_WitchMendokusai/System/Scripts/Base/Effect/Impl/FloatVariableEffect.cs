using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class FloatVariableEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			FloatVariable targetStat = effectInfo.Data as FloatVariable;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			targetStat.RuntimeValue = Arithmetic.Calc(targetStat.RuntimeValue, value, arithmeticOperator);
		}
	}
}