namespace WitchMendokusai
{
	public class DungeonStatEffect : IEffect
	{
		private DungeonStat DungeonStat => DataManager.Instance.DungeonStat;

		public void Apply(EffectInfo effectInfo)
		{
			DungeonStatType Type = (effectInfo.Data as DungeonStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(DungeonStat[Type], value, arithmeticOperator);
			DungeonStat[Type] = newValue;
		}
	}
}