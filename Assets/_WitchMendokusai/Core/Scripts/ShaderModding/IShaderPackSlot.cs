using UnityEngine;

namespace WitchMendokusai
{
	public interface IShaderPackSlot
	{
		string SlotId { get; }
		void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo);
		void Revert();
	}
}
