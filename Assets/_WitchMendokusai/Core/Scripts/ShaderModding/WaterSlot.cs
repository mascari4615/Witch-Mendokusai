using UnityEngine;

namespace WitchMendokusai
{
	// P2 — Water slot. 호수/바다 GameObject 의 MeshRenderer.sharedMaterial 교체.
	public class WaterSlot : IShaderPackSlot
	{
		public const string SLOT_ID = "water";

		public string SlotId => SLOT_ID;

		public void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo)
		{
			// P2-C 에서 구현 — 호수/바다 MeshRenderer 식별 + sharedMaterial 교체
		}

		public void Revert()
		{
			// P2-C 에서 구현 — sharedMaterial 복원
		}
	}
}
