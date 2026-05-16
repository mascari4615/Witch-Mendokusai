using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-1 — IContextualEffect dual. runner 경로 = ctx.ObjectPoolManager.
	// 구 IEffect 경로 = ObjectPoolManagerBridge transitional (정적 SO 호출처 — 후속 수렴 시 제거).
	public class SpawnObjectEffect : IContextualEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			GameObject prefab = (effectInfo.Data as ObjectData).GameObject;
			GameObject instance = ObjectPoolManagerBridge.Spawn(prefab);
			instance.SetActive(true);
		}

		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			GameObject prefab = (effectInfo.Data as ObjectData).GameObject;
			GameObject instance = context.ObjectPoolManager.Spawn(prefab);
			instance.SetActive(true);
		}
	}
}