namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class StatEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			UnitStat playerStat = context.PlayerProvider.Current.UnitStat;
			UnitStatType type = (effectInfo.Data as UnitStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(playerStat[type], value, arithmeticOperator);
			playerStat[type] = newValue;
		}
	}
}
