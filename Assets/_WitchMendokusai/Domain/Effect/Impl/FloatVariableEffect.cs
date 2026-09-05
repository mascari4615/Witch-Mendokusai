using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (ctx 불요 Effect — context 무시).
	public class FloatVariableEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			FloatVariable targetStat = effectInfo.Data as FloatVariable;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			targetStat.RuntimeValue = Arithmetic.Calc(targetStat.RuntimeValue, value, arithmeticOperator);
		}
	}
}