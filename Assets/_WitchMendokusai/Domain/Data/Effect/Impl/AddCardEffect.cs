namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2A first-use — EffectContext seam 입증 (dead interface 회피).
	// IContextualEffect 경로(DI runner) = ctx.SOManager. 구 IEffect 경로(정적 SO 호출처) = SOManagerBridge transitional.
	// Slice 2 후속서 SO 호출처도 runner 수렴 → 구 Apply(EffectInfo) + SOManagerBridge 제거.
	public class AddCardEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			CardData targetCard = effectInfo.Data as CardData;
			SOManagerBridge.SelectedCardBuffer.Add(targetCard);
		}

		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			CardData targetCard = effectInfo.Data as CardData;
			context.SOManager.SelectedCardBuffer.Add(targetCard);
		}
	}
}
