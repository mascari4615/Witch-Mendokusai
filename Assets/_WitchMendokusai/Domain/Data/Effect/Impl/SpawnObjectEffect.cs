using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch (IContextualEffect dual 폐기, static Bridge 0).
	public class SpawnObjectEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			GameObject prefab = (effectInfo.Data as ObjectData).GameObject;
			GameObject instance = context.ObjectPoolManager.Spawn(prefab);
			instance.SetActive(true);
		}
	}
}
