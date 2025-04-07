using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct RuntimeCriteriaSaveData
	{
		public CriteriaInfoSaveData CriteriaInfo;
		public bool JustOnce;
		public bool IsCompleted;
	}
}