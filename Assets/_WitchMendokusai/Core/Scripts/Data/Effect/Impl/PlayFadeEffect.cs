using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public class PlayFadeEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			UIManager uiManager = UIManager.Instance;
			if (uiManager == null || uiManager.Transition == null)
			{
				Debug.LogWarning("[PlayFadeEffect] UIManager.Transition 미존재");
				return;
			}

			// EffectInfo.Value (durationMs) 는 prototype 단계에서 *기록만* — TransitionView 가 USS const 시간 사용.
			// 정사 단계 (FadeRunner 분리) 에서 duration 파라미터화 예정.
			uiManager.Transition.Transition(() => { }).Forget();
		}
	}
}
