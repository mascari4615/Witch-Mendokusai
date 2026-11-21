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

			// 현재 체력 더하기는 회복. 회복 감소 제약과 회복 연출을 같은 길로
			if (type == UnitStatType.HP_CUR && arithmeticOperator == ArithmeticOperator.Add && value > 0)
			{
				context.PlayerProvider.CurrentObject.ReceiveHeal(value);
				return;
			}

			int newValue = (int)Arithmetic.Calc(playerStat[type], value, arithmeticOperator);
			playerStat[type] = newValue;
		}
	}
}
