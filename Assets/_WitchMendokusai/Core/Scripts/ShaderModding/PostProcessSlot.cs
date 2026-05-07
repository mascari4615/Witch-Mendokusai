using UnityEngine;

namespace WitchMendokusai
{
	// base+overlay 합성 — 셰이더팩이 DefaultVolumeProfile 을 수정하지 않고 별도 Runtime Volume 에 합성된다.
	public class PostProcessSlot : IShaderPackSlot
	{
		public const string SLOT_ID = "postprocess";

		public string SlotId => SLOT_ID;

		public void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo)
		{
		}

		public void Revert()
		{
		}
	}
}
