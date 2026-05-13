using UnityEngine;

namespace WitchMendokusai
{
	public class SpawnObjectEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			GameObject prefab = (effectInfo.Data as ObjectData).GameObject;
			GameObject instance = ObjectPoolManagerBridge.Spawn(prefab);
			instance.SetActive(true);
		}
	}
}