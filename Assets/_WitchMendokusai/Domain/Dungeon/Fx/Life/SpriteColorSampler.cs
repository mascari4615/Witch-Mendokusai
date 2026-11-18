using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 스프라이트의 대표 색 (불투명 부분 평균). 처치 파편 색을 몬스터 색에 맞추는 용도
	/// - 그림 영역을 작은 렌더 텍스처로 줄이고 GPU 에서 비동기로 읽음. 텍스처 Read/Write 설정 불필요, 프레임 멈춤 없음
	/// - 그림마다 한 번만 읽고 기억. 결과가 오기 전 (첫 처치) 에는 false, 부르는 쪽이 기본 색
	/// - 비동기 읽기를 못 하는 기기는 늘 false
	/// </summary>
	public static class SpriteColorSampler
	{
		// 줄인 그림의 한 변 (화소). 평균 색만 보므로 작아도 충분
		private const int SAMPLE_SIZE = 16;

		private static readonly Dictionary<Sprite, Color> sampled = new();
		private static readonly HashSet<Sprite> pending = new();

		public static bool TryGet(Sprite sprite, out Color color)
		{
			if (sampled.TryGetValue(sprite, out color))
				return true;
			if (pending.Contains(sprite) == false && SystemInfo.supportsAsyncGPUReadback)
				Request(sprite);
			return false;
		}

		private static void Request(Sprite sprite)
		{
			// 아틀라스에 빽빽이 묶인 그림도 되게 textureRect 대신 uv 범위
			Vector2[] uvs = sprite.uv;
			Vector2 uvMin = uvs[0];
			Vector2 uvMax = uvs[0];
			for (int i = 1; i < uvs.Length; i++)
			{
				uvMin = Vector2.Min(uvMin, uvs[i]);
				uvMax = Vector2.Max(uvMax, uvs[i]);
			}

			RenderTexture target = RenderTexture.GetTemporary(SAMPLE_SIZE, SAMPLE_SIZE, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			Graphics.Blit(sprite.texture, target, uvMax - uvMin, uvMin);
			pending.Add(sprite);
			AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
			{
				RenderTexture.ReleaseTemporary(target);
				pending.Remove(sprite);
				sampled[sprite] = request.hasError ? Color.white : AverageOpaque(request.GetData<Color32>());
			});
		}

		// 알파로 가중한 평균. 투명한 바탕은 빠짐
		private static Color AverageOpaque(NativeArray<Color32> pixels)
		{
			float red = 0f;
			float green = 0f;
			float blue = 0f;
			float weight = 0f;
			for (int i = 0; i < pixels.Length; i++)
			{
				Color32 pixel = pixels[i];
				float alpha = pixel.a / 255f;
				red += pixel.r / 255f * alpha;
				green += pixel.g / 255f * alpha;
				blue += pixel.b / 255f * alpha;
				weight += alpha;
			}
			if (weight <= 0f)
				return Color.white;
			return new Color(red / weight, green / weight, blue / weight, 1f);
		}
	}
}
