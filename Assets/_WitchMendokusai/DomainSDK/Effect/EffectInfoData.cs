using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct EffectInfoData
	{
		public EffectType Type;
		public int DataSoID;
		public ArithmeticOperator ArithmeticOperator;
		public int Value;
	}
}
