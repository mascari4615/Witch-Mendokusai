using System.Collections.Generic;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-2 — IContextualEffect dual. runner 경로 = ctx.DataManager.
	// 구 IEffect 경로 = DataManagerBridge transitional (정적 SO 호출처 — Slice 3-3 수렴 시 제거).
	public class UnlockRecipeEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo) => Apply(effectInfo, DataManagerBridge.IsRecipeUnlocked);

		public void Apply(EffectInfo effectInfo, EffectContext context) => Apply(effectInfo, context.DataManager.IsRecipeUnlocked);

		private static void Apply(EffectInfo effectInfo, Dictionary<int, bool> isRecipeUnlocked)
			=> isRecipeUnlocked[(effectInfo.Data as ItemData).ID] = true;
	}
}