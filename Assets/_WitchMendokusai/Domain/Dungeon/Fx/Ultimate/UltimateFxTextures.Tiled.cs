using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 이어 붙여도 이음새가 없는 반복 무늬 (Repeat). UV 를 여러 바퀴 감거나 흘려 쓰는 메시 (불길 뼈대 원뿔, 강착 원반) 용
	/// - 가장자리가 투명한 Smoke, Soft 를 감아 쓰면 둘레 대부분이 빈 칸으로 보임 (독립 검토 2026-11-21)
	/// </summary>
	public static partial class UltimateFxTextures
	{
		/// <summary> 잡음 무늬, 가로 (u) 로 촘촘하고 세로 (v) 로 성김. 알파는 0.05~1 (바닥이 높으면 고르게 희뿌옇게 보임). 원뿔은 길이 방향, 고리판은 둘레 방향 줄무늬가 됨 </summary>
		public static Texture2D TiledStreaks => Get("tiledStreaks", 256, 256, BuildTiledStreaks, TextureWrapMode.Repeat);

		// 네 귀퉁이를 섞어 이음새를 지운 잡음
		private static void BuildTiledStreaks(int width, int height, Color[] pixels)
		{
			const float SCALE_X = 16f;
			const float SCALE_Y = 4f;
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float u = column / (float)width;
				float v = row / (float)height;
				float noise = Mathf.Lerp(
					Mathf.Lerp(Fbm(u * SCALE_X, v * SCALE_Y, 211, 4), Fbm((u - 1f) * SCALE_X, v * SCALE_Y, 211, 4), u),
					Mathf.Lerp(Fbm(u * SCALE_X, (v - 1f) * SCALE_Y, 211, 4), Fbm((u - 1f) * SCALE_X, (v - 1f) * SCALE_Y, 211, 4), u),
					v);
				float alpha = Mathf.Lerp(0.05f, 1f, Mathf.Clamp01((noise - 0.35f) * 2.6f));
				pixels[row * width + column] = new Color(1f, 1f, 1f, alpha);
			}
		}
	}
}
