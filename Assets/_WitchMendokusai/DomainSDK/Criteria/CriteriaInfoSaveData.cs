using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct CriteriaInfoSaveData
	{
		public CriteriaType Type;
		public int DataID;
		public ComparisonOperator ComparisonOperator;
		public int Value;
		public bool JustOnce;
	}
}
