using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct EffectInfo
	{
		public EffectType Type;
		public DataSO Data;
		public ArithmeticOperator ArithmeticOperator;
		public int Value;
	}
}
