using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// base+overlay 합성 — 셰이더팩이 DefaultVolumeProfile 을 수정하지 않고 별도 Runtime Volume 에 합성된다.
	// 모더는 AssetBundle 안에 VolumeProfile.asset 박고 manifest.slots[].assetName 으로 명시.
	public class PostProcessSlot : IShaderPackSlot
	{
		public const string SLOT_ID = "postprocess";
		public const string RUNTIME_VOLUME_OBJECT_NAME = "ShaderPack.PostProcess.RuntimeVolume";

		public string SlotId => SLOT_ID;

		private GameObject runtimeVolumeObject;
		private Volume runtimeVolume;

		public void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo)
		{
			VolumeProfile profile = bundle.LoadAsset<VolumeProfile>(slotInfo.assetName);
			if (profile == null)
			{
				Debug.LogError($"[PostProcessSlot] VolumeProfile '{slotInfo.assetName}' not found in bundle.");
				return;
			}

			Revert();

			runtimeVolumeObject = new GameObject(RUNTIME_VOLUME_OBJECT_NAME);
			Object.DontDestroyOnLoad(runtimeVolumeObject);

			runtimeVolume = runtimeVolumeObject.AddComponent<Volume>();
			runtimeVolume.isGlobal = true;
			runtimeVolume.priority = slotInfo.priority;
			runtimeVolume.sharedProfile = profile;

			Debug.Log($"[PostProcessSlot] Applied VolumeProfile '{slotInfo.assetName}' (priority {slotInfo.priority})");
		}

		public void Revert()
		{
			if (runtimeVolumeObject == null)
				return;

			Object.Destroy(runtimeVolumeObject);
			runtimeVolumeObject = null;
			runtimeVolume = null;

			Debug.Log($"[PostProcessSlot] Reverted runtime volume.");
		}
	}
}
