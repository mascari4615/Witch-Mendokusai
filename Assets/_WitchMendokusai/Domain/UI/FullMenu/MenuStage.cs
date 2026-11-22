using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴 결의 화면 (ESC 메뉴, 업적) 이 같이 쓰는 무대 도구. 1080 기준 USS 무대를 화면에 맞추고, 둥근 빛 그림을 만듦
	/// </summary>
	public static class MenuStage
	{
		/// <summary>
		/// USS 기준 화면 (높이 referenceHeight) 을 area 높이에 맞춤. 너비는 화면 비율만큼 늘려 양쪽 칸이 가장자리에 붙음.
		/// 기준 최소 너비보다 좁은 화면은 너비에 맞추고 세로를 늘림
		/// </summary>
		public static void Fit(VisualElement area, VisualElement stage, float referenceHeight, float minimumWidth)
		{
			float width = area.resolvedStyle.width;
			float height = area.resolvedStyle.height;
			if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f)
			{
				return;
			}

			float scale = height / referenceHeight;
			if (width / scale < minimumWidth)
			{
				scale = width / minimumWidth;
			}
			stage.style.width = width / scale;
			stage.style.height = height / scale;
			stage.style.scale = new Scale(new Vector3(scale, scale, 1f));
		}

		/// <summary>둥근 빛. UI Toolkit 에 원형 그라데이션이 없어 작은 그림으로. 색은 USS tint. 다 쓰면 부른 쪽이 Destroy</summary>
		public static Texture2D CreateGlowTexture(int size, string textureName)
		{
			Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
			{
				name = textureName,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave,
			};
			Color32[] pixels = new Color32[size * size];
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
					float falloff = Mathf.Clamp01(1f - distance);
					byte alpha = (byte)Mathf.RoundToInt(falloff * falloff * byte.MaxValue);
					pixels[y * size + x] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, true);
			return texture;
		}
	}
}
