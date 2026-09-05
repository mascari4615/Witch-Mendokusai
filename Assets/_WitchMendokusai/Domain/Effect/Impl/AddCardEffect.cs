namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class AddCardEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			CardData targetCard = effectInfo.Data as CardData;
			context.SOManager.SelectedCardBuffer.Add(targetCard);
		}
	}
}
