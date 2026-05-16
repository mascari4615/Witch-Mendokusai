using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-2 — IContextualEffect dual. runner 경로 = ctx.DataManager.
	// 구 IEffect 경로 = DataManagerBridge transitional (정적 SO 호출처 — Slice 3-3 수렴 시 제거).
	public class GameStatEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo) => Apply(effectInfo, DataManagerBridge.GameStat);

		public void Apply(EffectInfo effectInfo, EffectContext context) => Apply(effectInfo, context.DataManager.GameStat);

		private static void Apply(EffectInfo effectInfo, GameStat gameStat)
		{
			GameStatType type = (effectInfo.Data as GameStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(gameStat[type], value, arithmeticOperator);
			gameStat[type] = newValue;
		}
	}
}