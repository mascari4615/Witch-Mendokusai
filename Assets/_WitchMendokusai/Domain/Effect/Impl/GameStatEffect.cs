namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class GameStatEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			GameStat gameStat = context.DataManager.GameStat;
			GameStatType type = (effectInfo.Data as GameStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(gameStat[type], value, arithmeticOperator);
			gameStat[type] = newValue;
		}
	}
}
