using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// P2 — Water slot. WaterRenderer 마커가 붙은 MeshRenderer 들의 sharedMaterial 을 모더 Material 로 교체.
	public class WaterSlot : IShaderPackSlot
	{
		public const string SLOT_ID = "water";

		public string SlotId => SLOT_ID;

		private readonly Dictionary<MeshRenderer, Material> originalMaterials = new();

		public void Apply(AssetBundle bundle, ShaderPackSlotInfo slotInfo)
		{
			Material modderMaterial = bundle.LoadAsset<Material>(slotInfo.assetName);
			if (modderMaterial == null)
			{
				Debug.LogError($"[WaterSlot] Material '{slotInfo.assetName}' not found in bundle.");
				return;
			}

			Revert();

			WaterRenderer[] waterRenderers = Object.FindObjectsByType<WaterRenderer>(FindObjectsSortMode.None);
			if (waterRenderers.Length == 0)
			{
				Debug.LogWarning($"[WaterSlot] No WaterRenderer found in scene. Material '{slotInfo.assetName}' has no target.");
				return;
			}

			foreach (WaterRenderer waterRenderer in waterRenderers)
			{
				MeshRenderer meshRenderer = waterRenderer.GetComponent<MeshRenderer>();
				if (meshRenderer == null)
					continue;

				originalMaterials[meshRenderer] = meshRenderer.sharedMaterial;
				meshRenderer.sharedMaterial = modderMaterial;
			}

			Debug.Log($"[WaterSlot] Applied Material '{slotInfo.assetName}' to {originalMaterials.Count} WaterRenderer(s).");
		}

		public void Revert()
		{
			if (originalMaterials.Count == 0)
				return;

			foreach (KeyValuePair<MeshRenderer, Material> pair in originalMaterials)
			{
				if (pair.Key == null)
					continue;
				pair.Key.sharedMaterial = pair.Value;
			}

			Debug.Log($"[WaterSlot] Reverted {originalMaterials.Count} WaterRenderer(s).");
			originalMaterials.Clear();
		}
	}
}
