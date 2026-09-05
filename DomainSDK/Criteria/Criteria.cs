using System;

namespace WitchMendokusai
{
	// [SerializeReference] 로 인스펙터에 담기려면(RuleEntry.criteria) 다형 참조라도 [Serializable] 이 필요하다.
	[Serializable]
	public abstract class Criteria : ICriteria
	{
		public abstract int GetCurValue();
		public abstract int GetTargetValue();
		public abstract bool Evaluate();
		public virtual float GetProgress()
		{
			return (float)GetCurValue() / GetTargetValue();
		}
	}
}
