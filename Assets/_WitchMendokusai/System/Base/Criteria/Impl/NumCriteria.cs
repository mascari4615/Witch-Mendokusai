namespace WitchMendokusai
{
	public abstract class NumCriteria : Criteria
	{
		public ComparisonOperator ComparisonOperator { get; private set; }
		public int TargetValue { get; private set; }

		public NumCriteria(CriteriaInfo criteriaInfo)
		{
			ComparisonOperator = criteriaInfo.ComparisonOperator;
			TargetValue = criteriaInfo.Value;
		}

		public override bool Evaluate()
		{
			return Evaluate_(GetCurValue());
		}

		protected bool Evaluate_(int curValue)
		{
			return ComparisonOperator switch
			{
				ComparisonOperator.Equal => curValue == TargetValue,
				ComparisonOperator.NotEqual => curValue != TargetValue,
				ComparisonOperator.GreaterThan => curValue > TargetValue,
				ComparisonOperator.LessThan => curValue < TargetValue,
				ComparisonOperator.GreaterThanOrEqualTo => curValue >= TargetValue,
				ComparisonOperator.LessThanOrEqualTo => curValue <= TargetValue,
				_ => throw new System.ArgumentOutOfRangeException(),
			};
		}

		public override float GetProgress()
		{
			return GetProgress_(GetCurValue());
		}

		public float GetProgress_(int curValue)
		{
			return (float)curValue / TargetValue;
		}
		
		public override int GetTargetValue()
		{
			return TargetValue;
		}
	}
}