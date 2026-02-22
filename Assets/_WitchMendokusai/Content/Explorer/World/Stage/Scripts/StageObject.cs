using UnityEngine;

namespace WitchMendokusai
{
	public class StageObject : MonoBehaviour
	{
		public Portal[] Portals => transform.GetComponentsInChildren<Portal>();

		private void OnEnable()
		{
			foreach (Portal portal in Portals)
				portal.Active();
		}

#if UNITY_EDITOR
		[ContextMenu(nameof(GenerateLightmapInfo))]
		public void GenerateLightmapInfo()
		{
			PrefabLightmapData.GenerateLightmapInfo();
		}
#endif
	}
}