using System;
using UnityEngine;

namespace WitchMendokusai
{
	public enum ArithmeticOperator
	{
		Set,
		Add,
		Subtract,
		Multiply,
		Divide,
		Remainder,
		Power
	}

	public class Arithmetic
	{
		public static float Calc(float a, float b, ArithmeticOperator arithmeticOperator)
		{
			return arithmeticOperator switch
			{
				ArithmeticOperator.Set => b,
				ArithmeticOperator.Add => a + b,
				ArithmeticOperator.Subtract => a - b,
				ArithmeticOperator.Multiply => a * b,
				ArithmeticOperator.Divide => a / b,
				ArithmeticOperator.Remainder => a % b,
				ArithmeticOperator.Power => (float)Mathf.Pow(a, b),
				_ => throw new ArgumentOutOfRangeException(),
			};
		}
	}
}