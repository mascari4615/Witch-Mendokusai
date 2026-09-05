using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (ctx 불요 Effect — context 무시).
	public class IntVariableEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			IntVariable targetStat = effectInfo.Data as IntVariable;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			targetStat.RuntimeValue = (int)Arithmetic.Calc(targetStat.RuntimeValue, value, arithmeticOperator);
		}
	}
}