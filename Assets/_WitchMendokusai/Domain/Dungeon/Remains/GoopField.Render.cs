using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 액체 그림. 칸 하나가 화소 하나인 텍스처 두 장 (색과 양, 불과 감전) 을 바닥 판이 부드럽게 그림
	public sealed partial class GoopField
	{
		private static readonly int GOOP_TEX_ID = Shader.PropertyToID("_GoopTex");
		private static readonly int HEAT_TEX_ID = Shader.PropertyToID("_HeatTex");
		private static readonly int FIRE_COLOR_ID = Shader.PropertyToID("_FireColor");
		private static readonly int SHOCK_COLOR_ID = Shader.PropertyToID("_ShockColor");
		private static readonly int TEXEL_COUNT_ID = Shader.PropertyToID("_TexelCount");

		private Transform plane;
		private Material material;
		private Texture2D goopTexture;
		private Texture2D heatTexture;
		private Color32[] goopPixels;
		private Color32[] heatPixels;
		private bool dirty = true;
		private float redrawTimer;

		private void CreateRenderer(Transform parent)
		{
			material = new Material(settings.GoopShader) { hideFlags = HideFlags.HideAndDontSave };
			GameObject planeObject = new("Goop");
			planeObject.transform.SetParent(parent, false);
			planeObject.AddComponent<MeshFilter>().sharedMesh = GroundStainCanvas.BuildPlane();
			MeshRenderer renderer = planeObject.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = material;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			plane = planeObject.transform;
		}

		private void MarkDirty() => dirty = true;

		private void EnsureTextures(int texels)
		{
			if (goopTexture != null && goopTexture.width == texels)
				return;
			DestroyTextures();
			goopTexture = new Texture2D(texels, texels, TextureFormat.RGBA32, false) { name = "GoopColor", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			heatTexture = new Texture2D(texels, texels, TextureFormat.RGBA32, false) { name = "GoopHeat", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			goopPixels = new Color32[texels * texels];
			heatPixels = new Color32[texels * texels];
			dirty = true;
		}

		// 마르는 칸은 양이 매 프레임 줄어서 간격마다 다시 씀. 칸이 없으면 판을 끔
		private void TickRender(float deltaTime, Vector3 focus, float groundHeight)
		{
			bool visible = cells.Count > 0;
			if (plane.gameObject.activeSelf != visible)
				plane.gameObject.SetActive(visible);
			if (visible == false)
				return;
			redrawTimer += deltaTime;
			if (dirty == false && redrawTimer < settings.GoopRedrawInterval)
				return;
			redrawTimer = 0f;
			dirty = false;

			int texels = Mathf.Max(8, Mathf.RoundToInt(settings.GoopViewSize / settings.GoopCellSize));
			EnsureTextures(texels);
			Vector2Int origin = CellOf(focus) - new Vector2Int(texels / 2, texels / 2);
			System.Array.Clear(goopPixels, 0, goopPixels.Length);
			System.Array.Clear(heatPixels, 0, heatPixels.Length);
			foreach (KeyValuePair<Vector2Int, Cell> pair in cells)
			{
				Vector2Int local = pair.Key - origin;
				if (local.x < 0 || local.y < 0 || local.x >= texels || local.y >= texels)
					continue;
				Cell cell = pair.Value;
				float wet = cell.Amount * (1f - Mathf.Clamp01(cell.Age / cell.Kind.DrySeconds));
				Color color = cell.Kind.Color;
				int index = local.y * texels + local.x;
				goopPixels[index] = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(wet));
				float burn = cell.BurnLeft > 0f ? Mathf.Clamp01(cell.BurnLeft / settings.FireBurnSeconds * 2f) : 0f;
				float shock = cell.ShockLeft > 0f ? Mathf.Clamp01(cell.ShockLeft / settings.ShockSeconds) : 0f;
				heatPixels[index] = new Color(burn, shock, 0f, 0f);
			}
			goopTexture.SetPixels32(goopPixels);
			goopTexture.Apply(false);
			heatTexture.SetPixels32(heatPixels);
			heatTexture.Apply(false);

			float size = texels * settings.GoopCellSize;
			plane.SetPositionAndRotation(new Vector3(origin.x * settings.GoopCellSize + size * .5f, groundHeight, origin.y * settings.GoopCellSize + size * .5f), Quaternion.identity);
			plane.localScale = new Vector3(size, 1f, size);
			material.SetTexture(GOOP_TEX_ID, goopTexture);
			material.SetTexture(HEAT_TEX_ID, heatTexture);
			material.SetColor(FIRE_COLOR_ID, settings.FireColor);
			material.SetColor(SHOCK_COLOR_ID, settings.ShockColor);
			material.SetFloat(TEXEL_COUNT_ID, texels);
		}

		private void DestroyTextures()
		{
			if (goopTexture != null)
				Object.Destroy(goopTexture);
			if (heatTexture != null)
				Object.Destroy(heatTexture);
			goopTexture = null;
			heatTexture = null;
		}

		public void Dispose()
		{
			DestroyTextures();
			Object.Destroy(material);
			if (plane != null)
				Object.Destroy(plane.gameObject);
		}
	}
}
