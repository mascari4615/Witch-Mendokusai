using UnityEngine;

namespace WitchMendokusai
{
	public class UnlockRecipeEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			DataManager.Instance.IsRecipeUnlocked[(effectInfo.Data as ItemData).ID] = true;
		}
	}
}