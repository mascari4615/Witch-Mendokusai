namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-2 — IContextualEffect dual. runner 경로 = ctx.DataManager.
	// 구 IEffect 경로 = DataManagerBridge transitional (정적 SO 호출처 — Slice 3-3 수렴 시 제거).
	public class DungeonStatEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo) => Apply(effectInfo, DataManagerBridge.DungeonStat);

		public void Apply(EffectInfo effectInfo, EffectContext context) => Apply(effectInfo, context.DataManager.DungeonStat);

		private static void Apply(EffectInfo effectInfo, DungeonStat dungeonStat)
		{
			DungeonStatType type = (effectInfo.Data as DungeonStatData).Type;
			int value = effectInfo.Value;
			ArithmeticOperator arithmeticOperator = effectInfo.ArithmeticOperator;

			int newValue = (int)Arithmetic.Calc(dungeonStat[type], value, arithmeticOperator);
			dungeonStat[type] = newValue;
		}
	}
}