namespace WitchMendokusai
{
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
