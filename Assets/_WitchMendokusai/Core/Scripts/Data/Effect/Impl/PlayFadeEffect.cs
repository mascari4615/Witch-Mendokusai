using UnityEngine;

namespace WitchMendokusai
{
	public class PlayFadeEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			Debug.Log($"[PlayFadeEffect] (stub) Play Fade: durationMs={effectInfo.Value}");
		}
	}
}
