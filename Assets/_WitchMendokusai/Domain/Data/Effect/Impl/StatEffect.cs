using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-1 — IContextualEffect dual. runner 경로 = ctx.PlayerProvider.
	// 구 IEffect 경로 = PlayerProviderBridge transitional (정적 SO 호출처 — 후속 수렴 시 제거).
	public class StatEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo) => Apply(effectInfo, PlayerProviderBridge.Current.UnitStat);

		public void Apply(EffectInfo effectInfo, EffectContext context) => Apply(effectInfo, context.PlayerProvider.Current.UnitStat);

		private static void Apply(EffectInfo effectInfo, UnitStat playerStat)
		{
			UnitStatType type = (effectInfo.Data as UnitStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(playerStat[type], value, arithmeticOperator);
			playerStat[type] = newValue;
		}
	}
}