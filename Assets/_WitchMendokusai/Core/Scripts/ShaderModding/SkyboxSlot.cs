using UnityEngine;

namespace WitchMendokusai
{
	// P2 — Skybox slot. RenderSettings.skybox 교체 + uniform input contract.
	// 모더 Skybox Material 이 SkyDirector 의 시간대 색 uniform 받아 합성:
	//   _WMSkyZenith / _WMSkyHorizon / _WMSkySun / _WMSkyStarAlpha / _WMSunAltitude / _WMSunDirection / _WMNormalizedTime
	// 자세한 contract: memo/wm/design/systems/shader-modding-architecture.md
	public class SkyboxSlot : IShaderPackSlot
	{
		public const string SLOT_ID = "skybox";

		public string SlotId => SLOT_ID;

		public void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo)
		{
			// P2-B 에서 구현 — RenderSettings.skybox 백업 + 모더 Material 적용
		}

		public void Revert()
		{
			// P2-B 에서 구현 — RenderSettings.skybox 복원
		}
	}
}
