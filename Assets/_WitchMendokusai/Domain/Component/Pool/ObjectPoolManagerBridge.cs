using UnityEngine;

namespace WitchMendokusai
{
	public static class ObjectPoolManagerBridge
	{
		private static ObjectPoolManager _instance;
		public static void Register(ObjectPoolManager objectPoolManager) => _instance = objectPoolManager;
		public static GameObject Spawn(GameObject prefab) => _instance.Spawn(prefab);
	}
}
