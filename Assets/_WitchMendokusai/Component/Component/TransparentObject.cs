using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class TransparentObject : MonoBehaviour
	{
		private const float TICK = .01f;
		private const float LERP_SPEED = 5f;
		private const float THRESHOLD_ALPHA = 0.25f;
		private const float THRESHOLD_MAX_TIMER = 0.5f;

		private MeshRenderer[] meshRenderers;
		private float timer = 0f;
		private Coroutine loop;

		private void Awake()
		{
			meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
		}

		public void UpdateTransparent()
		{
			timer = 0f;
			if (loop == null)
				loop = StartCoroutine(Loop());
		}

		private IEnumerator Loop()
		{
			yield return BecomeTransparent();
			yield return CheckTime();
			yield return ResetOriginalTransparent();
			loop = null;
		}
		
		private IEnumerator BecomeTransparent()
		{
			SetMaterialTransparent();
			yield return SetTransparency(THRESHOLD_ALPHA);
		}
		private IEnumerator ResetOriginalTransparent()
		{
			yield return SetTransparency(1f);
			SetMaterialOpaque();
		}
		private IEnumerator SetTransparency(float targetAlpha)
		{
			WaitForSeconds delay = new(TICK);
			for (float t = 0; t < 1; t += TICK * LERP_SPEED)
			{
				foreach (MeshRenderer meshR in meshRenderers)
				{
					foreach (Material material in meshR.materials)
					{
						Color color = material.color;
						color.a = Mathf.Lerp(color.a, targetAlpha, t);
						material.color = color;
					}
				}
				yield return delay;
			}
		}

		private IEnumerator CheckTime()
		{
			for (; timer < THRESHOLD_MAX_TIMER; timer += Time.deltaTime)
				yield return null;
		}

		private void SetMaterialTransparent() => SetMaterialRendering(1, 3, 3000);
		private void SetMaterialOpaque() => SetMaterialRendering(0, 0, -1);
		private void SetMaterialRendering(float surfaceValue, int renderingMode, int renderQueue)
		{
			foreach (MeshRenderer meshR in meshRenderers)
			{
				foreach (Material material in meshR.materials)
				{
					material.SetFloat("_Surface", surfaceValue);
					SetMaterialRenderingMode(material, renderingMode, renderQueue);
				}
			}
		}
		// 0 = Opaque, 1 = Cutout, 2 = Fade, 3 = Transparent
		private void SetMaterialRenderingMode(Material material, int mode, int renderQueue)
		{
			material.SetFloat("_Mode", mode);
			material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetInt("ZWrite", 0);
			material.DisableKeyword("_ALPHATEST_ON");
			material.EnableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = renderQueue;
		}
	}
}