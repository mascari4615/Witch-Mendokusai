using FMODUnity;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 적 정지 바닥 아이템. 임시 이름과 임시 값
	public class FreezeObject : GameItemObject
	{
		[Inject] internal ResourceManager resourceManager;

		protected override void OnEffect()
		{
			RuntimeManager.PlayOneShot("event:/SFX/EXP", transform.position);
			SurvivalRunRules rules = resourceManager.SurvivalRunRules;
			EnemyFreezeState.Start(rules.FreezeSeconds, rules.FreezeBossRatio);
			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.OnFreezePicked(transform.position);
		}
	}
}
