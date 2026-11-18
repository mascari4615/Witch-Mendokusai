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
	/// - 기억 수 상한 (cacheLimit, 진행 중 포함) 을 넘으면 새 그림은 안 읽음. 한 프레임 요청 수도 상한
	/// - 판이 끝나면 Clear. 지금은 MonsterLifeFxDriver.OnDestroy (씬 전환)
	/// - 비동기 읽기를 못 하는 기기는 늘 false
	/// </summary>
	public static class SpriteColorSampler
	{
		// 줄인 그림의 한 변 (화소). 평균 색만 보므로 작아도 충분
		private const int SAMPLE_SIZE = 16;

		private static readonly Dictionary<Sprite, Color> sampled = new();
		private static readonly HashSet<Sprite> pending = new();

		// Clear 뒤에 도착한 옛 결과를 버리는 세대 번호
		private static int generation;
		private static int requestFrame = -1;
		private static int requestsThisFrame;

		/// <summary> 기억한 색이 있으면 true. 없으면 상한 안에서 읽기를 요청하고 false </summary>
		public static bool TryGet(Sprite sprite, int cacheLimit, int requestsPerFrame, out Color color)
		{
			if (sampled.TryGetValue(sprite, out color))
				return true;
			if (pending.Contains(sprite) || SystemInfo.supportsAsyncGPUReadback == false)
				return false;
			if (sampled.Count + pending.Count >= cacheLimit)
				return false;

			int frame = Time.frameCount;
			if (frame != requestFrame)
			{
				requestFrame = frame;
				requestsThisFrame = 0;
			}
			if (requestsThisFrame >= requestsPerFrame)
				return false;

			requestsThisFrame++;
			Request(sprite);
			return false;
		}

		/// <summary> 기억과 진행 중 요청을 비움. 진행 중 요청의 결과는 도착해도 버림 </summary>
		public static void Clear()
		{
			sampled.Clear();
			pending.Clear();
			generation++;
			requestFrame = -1;
			requestsThisFrame = 0;
		}

		// 도메인 다시 읽기를 끈 Play 진입에서도 빈 상태로 시작
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetOnLoad() => Clear();

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
			RenderTexture previousActive = RenderTexture.active;
			Graphics.Blit(sprite.texture, target, uvMax - uvMin, uvMin);
			if (RenderTexture.active == target)
				RenderTexture.active = previousActive;

			int requestGeneration = generation;
			pending.Add(sprite);
			AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
			{
				if (RenderTexture.active == target)
					RenderTexture.active = null;
				RenderTexture.ReleaseTemporary(target);
				if (requestGeneration != generation)
					return;
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
