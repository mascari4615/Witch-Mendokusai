using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 플레이어 몸 색 번쩍임. 피격 직후 번쩍임과 무적 시간 깜빡임이 같이 씀
	/// - 실제로 보이는 그림 (MeshParent 아래 메시, 머리와 몸 스프라이트) 에 얹음. 옛 깜빡임은 꺼진 렌더러 (HeadAnchor) 재질에 써서 화면에 안 보였음 (2026-11-21)
	/// - 메시: 재질 기본색 (_BaseColor, 없으면 _Color) 을 속성 블록으로 곱함. 인형 색 (ApplyBodyTint) 이 같은 블록에 쓴 값을 쉬는 색으로 잡고 끝나면 그 값으로 되돌림
	/// - 스프라이트: 렌더러 색을 곱함. 1 을 못 넘어서 밝기 대신 색만
	/// </summary>
	public sealed class PlayerBodyFlash
	{
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

		private struct MeshSlot
		{
			public Renderer Renderer;
			public int MaterialIndex;
			public int PropertyId;
			public Color Rest;
		}

		private struct SpriteSlot
		{
			public SpriteRenderer Renderer;
			public Color Rest;
		}

		private readonly List<MeshSlot> meshSlots = new();
		private readonly List<SpriteSlot> spriteSlots = new();
		// MonoBehaviour 필드 초기화 (PlayerObject 생성자) 에서 만들면 Unity 가 막음. 처음 쓸 때 만듦
		private MaterialPropertyBlock block;
		private bool captured;

		public bool IsFlashing => captured;

		/// <summary> 세기 0 이면 쉬는 색. 잡은 그림은 Release 까지 유지 (깜빡임마다 다시 찾지 않게) </summary>
		public void Set(Transform meshParent, SpriteRenderer[] sprites, Color flashColor, float weight)
		{
			if (captured == false)
			{
				if (weight <= 0f)
					return;
				block ??= new MaterialPropertyBlock();
				Capture(meshParent, sprites);
			}
			weight = Mathf.Max(0f, weight);
			Color meshMultiplier = Color.Lerp(Color.white, flashColor, weight);
			meshMultiplier.a = 1f;
			for (int i = 0; i < meshSlots.Count; i++)
			{
				MeshSlot slot = meshSlots[i];
				if (slot.Renderer == null)
					continue;
				slot.Renderer.GetPropertyBlock(block, slot.MaterialIndex);
				block.SetColor(slot.PropertyId, slot.Rest * meshMultiplier);
				slot.Renderer.SetPropertyBlock(block, slot.MaterialIndex);
			}
			Color spriteMultiplier = Color.Lerp(Color.white, Normalized(flashColor), weight);
			for (int i = 0; i < spriteSlots.Count; i++)
			{
				SpriteSlot slot = spriteSlots[i];
				if (slot.Renderer == null)
					continue;
				Color color = slot.Rest * spriteMultiplier;
				color.a = slot.Rest.a;
				slot.Renderer.color = color;
			}
		}

		public void Release()
		{
			if (captured == false)
				return;
			captured = false;
			for (int i = 0; i < meshSlots.Count; i++)
			{
				MeshSlot slot = meshSlots[i];
				if (slot.Renderer == null)
					continue;
				slot.Renderer.GetPropertyBlock(block, slot.MaterialIndex);
				block.SetColor(slot.PropertyId, slot.Rest);
				slot.Renderer.SetPropertyBlock(block, slot.MaterialIndex);
			}
			for (int i = 0; i < spriteSlots.Count; i++)
			{
				if (spriteSlots[i].Renderer != null)
					spriteSlots[i].Renderer.color = spriteSlots[i].Rest;
			}
			meshSlots.Clear();
			spriteSlots.Clear();
		}

		private void Capture(Transform meshParent, SpriteRenderer[] sprites)
		{
			captured = true;
			meshSlots.Clear();
			spriteSlots.Clear();
			if (meshParent != null)
			{
				foreach (Renderer meshRenderer in meshParent.GetComponentsInChildren<Renderer>(false))
				{
					if (meshRenderer is SpriteRenderer || meshRenderer is ParticleSystemRenderer || meshRenderer.enabled == false)
						continue;
					Material[] materials = meshRenderer.sharedMaterials;
					for (int index = 0; index < materials.Length; index++)
					{
						Material material = materials[index];
						if (material == null)
							continue;
						int propertyId = material.HasProperty(BASE_COLOR_ID) ? BASE_COLOR_ID : material.HasProperty(COLOR_ID) ? COLOR_ID : -1;
						if (propertyId < 0)
							continue;
						meshRenderer.GetPropertyBlock(block, index);
						Color rest = block.HasColor(propertyId) ? block.GetColor(propertyId) : material.GetColor(propertyId);
						meshSlots.Add(new MeshSlot { Renderer = meshRenderer, MaterialIndex = index, PropertyId = propertyId, Rest = rest });
					}
				}
			}
			foreach (SpriteRenderer sprite in sprites)
			{
				if (sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy)
					spriteSlots.Add(new SpriteSlot { Renderer = sprite, Rest = sprite.color });
			}
		}

		private static Color Normalized(Color color)
		{
			float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
			if (max <= 1f)
				return color;
			return new Color(color.r / max, color.g / max, color.b / max, 1f);
		}
	}
}
