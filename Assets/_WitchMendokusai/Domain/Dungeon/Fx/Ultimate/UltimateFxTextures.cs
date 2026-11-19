using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 연출 텍스처를 코드로 한 번 구워 재사용. 에셋 파일이 아니라 첫 사용 때 구움
	/// - 이 파일: 굽기 도구 (캐시, 잡음, 거리 함수) 와 여러 연출이 같이 쓰는 모양 (빛, 불꽃, 연기, 고리, 속도선, 기둥, 파편)
	/// - 연출 주제 물체 (카드, 달, 은하 날, 블랙홀, 마법진, 가시, 광선) 는 UltimateFxTextures.Themes.cs, 지면 균열은 UltimateFxTextures.Crack.cs
	/// - 모양은 잡음 (fbm) 으로 가장자리를 흔들어 입자마다 다르게 보이게 함
	/// </summary>
	public static partial class UltimateFxTextures
	{
		private static readonly Dictionary<string, Texture2D> cache = new();

		public static Texture2D Soft => Get("soft", 128, 128, BuildSoft);
		public static Texture2D Spark => Get("spark", 128, 128, BuildSpark);
		public static Texture2D Smoke => Get("smoke", 256, 256, BuildSmoke);
		public static Texture2D Ring => Get("ring", 512, 512, BuildRing);
		public static Texture2D ThinRing => Get("thinRing", 512, 512, BuildThinRing);
		public static Texture2D Streaks => Get("streaks", 512, 512, BuildStreaks);
		public static Texture2D Pillar => Get("pillar", 128, 256, BuildPillar);
		public static Texture2D Line => Get("line", 16, 64, BuildLine);
		public static Texture2D Vignette => Get("vignette", 256, 256, BuildVignette);
		public static Texture2D White => Get("white", 4, 4, (width, height, pixels) => Fill(pixels, Color.white));

		public static Texture2D[] Shards
		{
			get
			{
				Texture2D[] result = new Texture2D[4];
				for (int i = 0; i < result.Length; i++)
				{
					int seed = i * 977 + 31;
					result[i] = Get("shard" + i, 64, 64, (width, height, pixels) => BuildShard(width, pixels, seed));
				}
				return result;
			}
		}

		// ---- 굽기 도구 ----
		private static Texture2D Get(string key, int width, int height, System.Action<int, int, Color[]> fill, TextureWrapMode wrap = TextureWrapMode.Clamp)
		{
			if (cache.TryGetValue(key, out Texture2D texture) && texture != null)
				return texture;
			Color[] pixels = new Color[width * height];
			fill(width, height, pixels);
			texture = Make(key, width, height, pixels, wrap);
			cache[key] = texture;
			return texture;
		}

		private static Texture2D Make(string name, int width, int height, Color[] pixels, TextureWrapMode wrap = TextureWrapMode.Clamp)
		{
			Texture2D texture = new(width, height, TextureFormat.RGBA32, true) { name = "UltimateFx_" + name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
			texture.SetPixels(pixels);
			texture.Apply(true);
			return texture;
		}

		private static void Fill(Color[] pixels, Color color)
		{
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = color;
		}

		/// <summary> 화소 칸을 -1~1 좌표로. 가운데가 0 </summary>
		private static Vector2 Centered(int column, int row, int width, int height)
			=> new(column / (width - 1f) * 2f - 1f, row / (height - 1f) * 2f - 1f);

		private static float Hash(int column, int row, int seed)
		{
			unchecked
			{
				int mixed = column * 374761393 + row * 668265263 + seed * 1274126177;
				mixed = (mixed ^ (mixed >> 13)) * 1274126177;
				mixed ^= mixed >> 16;
				return (mixed & 0xffff) / 65535f;
			}
		}

		private static float ValueNoise(float positionX, float positionY, int seed)
		{
			int cellX = Mathf.FloorToInt(positionX);
			int cellY = Mathf.FloorToInt(positionY);
			float blendX = positionX - cellX;
			float blendY = positionY - cellY;
			blendX = blendX * blendX * (3f - 2f * blendX);
			blendY = blendY * blendY * (3f - 2f * blendY);
			float corner00 = Hash(cellX, cellY, seed);
			float corner10 = Hash(cellX + 1, cellY, seed);
			float corner01 = Hash(cellX, cellY + 1, seed);
			float corner11 = Hash(cellX + 1, cellY + 1, seed);
			return Mathf.Lerp(Mathf.Lerp(corner00, corner10, blendX), Mathf.Lerp(corner01, corner11, blendX), blendY);
		}

		private static float Fbm(float positionX, float positionY, int seed, int octaves = 4)
		{
			float sum = 0f;
			float amplitude = 0.5f;
			float norm = 0f;
			for (int i = 0; i < octaves; i++)
			{
				sum += ValueNoise(positionX, positionY, seed + i * 17) * amplitude;
				norm += amplitude;
				positionX *= 2f;
				positionY *= 2f;
				amplitude *= 0.5f;
			}
			return sum / norm;
		}

		private static float SegmentDistance(Vector2 point, Vector2 start, Vector2 end)
		{
			Vector2 segment = end - start;
			float lengthSquared = segment.sqrMagnitude;
			float along = lengthSquared < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
			return (point - (start + segment * along)).magnitude;
		}

		private static float SignedPolygonDistance(Vector2 point, Vector2[] corners)
		{
			float minDistance = float.MaxValue;
			bool inside = false;
			for (int i = 0, j = corners.Length - 1; i < corners.Length; j = i++)
			{
				minDistance = Mathf.Min(minDistance, SegmentDistance(point, corners[i], corners[j]));
				bool crosses = (corners[i].y > point.y) != (corners[j].y > point.y);
				if (crosses && point.x < (corners[j].x - corners[i].x) * (point.y - corners[i].y) / (corners[j].y - corners[i].y) + corners[i].x)
					inside = inside == false;
			}
			return inside ? -minDistance : minDistance;
		}

		private static float Gauss(float distance, float width) => Mathf.Exp(-Square(distance / width));

		private static float Square(float value) => value * value;

		// ---- 공용 모양 ----
		private static void BuildSoft(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float radius = Centered(column, row, width, height).magnitude;
				float alpha = Mathf.Clamp01(1f - radius);
				alpha = alpha * alpha * (3f - 2f * alpha);
				pixels[row * width + column] = new Color(1f, 1f, 1f, alpha * alpha);
			}
		}

		private static void BuildSpark(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = point.magnitude;
				float core = Mathf.Pow(Mathf.Clamp01(1f - radius * 2.2f), 2f);
				float horizontal = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(point.x)), 6f) * Mathf.Exp(-Mathf.Abs(point.y) * 28f);
				float vertical = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(point.y)), 6f) * Mathf.Exp(-Mathf.Abs(point.x) * 28f);
				float halo = Mathf.Pow(Mathf.Clamp01(1f - radius), 3f) * 0.25f;
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + horizontal + vertical + halo));
			}
		}

		private static void BuildSmoke(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float radius = Centered(column, row, width, height).magnitude;
				float noise = Fbm(column / 28f, row / 28f, 5, 5);
				float edge = Mathf.Clamp01((1f - radius) * 1.6f);
				float alpha = Mathf.Clamp01((noise * 1.5f - 0.3f) * edge * edge);
				float shade = Mathf.Lerp(0.55f, 1f, Fbm(column / 20f + 9f, row / 20f, 11, 3));
				pixels[row * width + column] = new Color(shade, shade, shade, alpha);
			}
		}

		// 충격파 고리: 앞은 선명, 안쪽은 길게 끌리는 꼬리. 판 반지름의 0.86 자리
		private static void BuildRing(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = point.magnitude;
				float angle = Mathf.Atan2(point.y, point.x);
				float wobble = (Fbm(Mathf.Cos(angle) * 3f + 5f, Mathf.Sin(angle) * 3f + 5f, 3, 3) - 0.5f) * 0.06f;
				float offset = radius - (RING_RADIUS + wobble);
				float front = Gauss(offset, 0.024f);
				float tail = offset < 0f ? Mathf.Exp(offset / 0.14f) * 0.45f : 0f;
				float edgeCut = Mathf.Clamp01((1f - radius) * 12f);
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(front + tail) * edgeCut);
			}
		}

		/// <summary> 고리 텍스처 (Ring, ThinRing) 의 고리 반지름. 판 반지름 1 기준 </summary>
		public const float RING_RADIUS = 0.86f;

		// 얇은 고리: 크기 비교 기준선. 선 하나와 옅은 번짐
		private static void BuildThinRing(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float radius = Centered(column, row, width, height).magnitude;
				float offset = radius - RING_RADIUS;
				float alpha = Gauss(offset, 0.006f) + Gauss(offset, 0.03f) * 0.18f;
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * Mathf.Clamp01((1f - radius) * 30f));
			}
		}

		private static void BuildStreaks(int width, int height, Color[] pixels)
		{
			const int LINES = 150;
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = point.magnitude;
				float angle = (Mathf.Atan2(point.y, point.x) / (Mathf.PI * 2f) + 0.5f) * LINES;
				int index = Mathf.FloorToInt(angle);
				float fraction = angle - index;
				float strength = Hash(index, 3, 71);
				float length = Hash(index, 9, 13);
				float thickness = Mathf.Lerp(0.04f, 0.22f, Hash(index, 5, 29));
				float line = Mathf.Clamp01(1f - Mathf.Abs(fraction - 0.5f) / thickness);
				float inner = Mathf.Lerp(0.38f, 0.78f, length);
				float radial = Mathf.Clamp01((radius - inner) / 0.18f) * Mathf.Clamp01((1.02f - radius) / 0.15f);
				pixels[row * width + column] = new Color(1f, 1f, 1f, line * line * radial * Mathf.Lerp(0.3f, 1f, strength));
			}
		}

		// 세로 빛기둥. 아래가 굵고 위로 갈수록 가늘어짐
		private static void BuildPillar(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float across = column / (width - 1f) * 2f - 1f;
				float along = row / (height - 1f);
				float wobble = (Fbm(along * 6f, 2f, 41, 3) - 0.5f) * 0.18f;
				float spread = Mathf.Lerp(0.5f, 0.3f, along) + wobble;
				float core = Gauss(across, spread * 0.28f);
				float body = Gauss(across, spread) * 0.55f;
				float streak = Mathf.Lerp(0.55f, 1f, Fbm(across * 14f + 3f, along * 3f, 53, 3));
				float fadeTop = Mathf.Clamp01((1f - along) * 3f);
				float fadeBottom = Mathf.Clamp01(along * 10f);
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01((core + body) * streak) * fadeTop * fadeBottom);
			}
		}

		// 선 그리기 (LineRenderer) 용 단면. 가로가 길이 방향, 세로 가운데가 밝음
		private static void BuildLine(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float across = row / (height - 1f) * 2f - 1f;
				float alpha = Gauss(across, 0.22f) + Gauss(across, 0.6f) * 0.35f;
				float white = Gauss(across, 0.22f);
				pixels[row * width + column] = new Color(Mathf.Lerp(0.85f, 1f, white), Mathf.Lerp(0.85f, 1f, white), 1f, Mathf.Clamp01(alpha));
			}
		}

		private static void BuildVignette(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float radius = Centered(column, row, width, height).magnitude / 1.4142f;
				float alpha = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1f, radius)), 1.7f);
				pixels[row * width + column] = new Color(1f, 1f, 1f, alpha);
			}
		}

		private static void BuildShard(int size, Color[] pixels, int seed)
		{
			// 꼭짓점 일곱 개의 불규칙한 다각형. 왼쪽 위의 빛을 받는 음영과 밝은 가장자리
			const int CORNERS = 7;
			Vector2[] corners = new Vector2[CORNERS];
			for (int i = 0; i < CORNERS; i++)
			{
				float angle = (i + Hash(i, 1, seed) * 0.6f) / CORNERS * Mathf.PI * 2f;
				float radius = Mathf.Lerp(0.55f, 0.92f, Hash(i, 2, seed));
				corners[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.8f);
			}
			float half = (size - 1) * 0.5f;
			for (int row = 0; row < size; row++)
			for (int column = 0; column < size; column++)
			{
				Vector2 point = Centered(column, row, size, size);
				float signedDistance = SignedPolygonDistance(point, corners);
				float alpha = Mathf.Clamp01(-signedDistance * half * 0.5f + 0.5f);
				float light = Mathf.Clamp01(0.55f + (-point.x * 0.35f + point.y * 0.35f) + (Fbm(column / 6f, row / 6f, seed, 3) - 0.5f) * 0.5f);
				float rim = Mathf.Clamp01(1f - Mathf.Abs(signedDistance) * 7f) * 0.35f;
				float shade = Mathf.Clamp01(light + rim);
				pixels[row * size + column] = new Color(shade, shade, shade, alpha);
			}
		}
	}
}
