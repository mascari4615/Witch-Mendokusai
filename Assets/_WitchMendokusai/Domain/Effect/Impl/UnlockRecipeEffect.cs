namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class UnlockRecipeEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
			=> context.DataManager.IsRecipeUnlocked[(effectInfo.Data as ItemData).ID] = true;
	}
}
