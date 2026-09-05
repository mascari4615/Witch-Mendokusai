using UnityEngine;

namespace WitchMendokusai
{
	// P2 — Skybox slot. RenderSettings.skybox 교체 + uniform input contract.
	// 모더 Skybox Material 이 SkyDirector 의 시간대 색 uniform 받아 합성:
	//   _WMSkyZenith / _WMSkyHorizon / _WMSkySun / _WMSkyStarAlpha / _WMSunAltitude / _WMSunDirection / _WMNormalizedTime
	// 자세한 contract: memo/wm/design/systems/shader-modding-architecture.md
	// uniform 노출: TASK-WM-054-C sub-C6 (SkyDirector.ApplyShaderGlobals)
	public class SkyboxSlot : IShaderPackSlot
	{
		public const string SLOT_ID = "skybox";

		public string SlotId => SLOT_ID;

		private Material originalSkybox;
		private bool hasBackup;

		public void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo)
		{
			Material modderMaterial = bundle.LoadAsset<Material>(slotInfo.assetName);
			if (modderMaterial == null)
			{
				Debug.LogError($"[SkyboxSlot] Material '{slotInfo.assetName}' not found in bundle.");
				return;
			}

			Revert();

			originalSkybox = RenderSettings.skybox;
			hasBackup = true;

			RenderSettings.skybox = modderMaterial;
			DynamicGI.UpdateEnvironment();

			Debug.Log($"[SkyboxSlot] Applied Material '{slotInfo.assetName}' (backup: '{(originalSkybox != null ? originalSkybox.name : "null")}').");
		}

		public void Revert()
		{
			if (hasBackup == false)
				return;

			RenderSettings.skybox = originalSkybox;
			DynamicGI.UpdateEnvironment();

			Debug.Log($"[SkyboxSlot] Reverted to '{(originalSkybox != null ? originalSkybox.name : "null")}'.");

			originalSkybox = null;
			hasBackup = false;
		}
	}
}
