namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class DungeonStatEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			DungeonStat dungeonStat = context.DataManager.DungeonStat;
			DungeonStatType type = (effectInfo.Data as DungeonStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(dungeonStat[type], value, arithmeticOperator);
			dungeonStat[type] = newValue;
		}
	}
}
