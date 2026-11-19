using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 지면 균열 두 장. Core 하얀 속 선, Glow 빛 번짐. 시드마다 다른 모양
	/// - 가지를 재귀로 뻗어 선분을 만들고 선분 거리로 그림. 곧게 뻗다가 모서리에서 꺾이는 각진 선
	/// - 그을음 판은 없앰. 넓은 검은 테두리가 식은 뒤 풀밭 위에서 갈색 나무뿌리로 읽혔음 (2026-11-19 검토)
	/// </summary>
	public static partial class UltimateFxTextures
	{
		private struct Segment
		{
			public Vector2 Start;
			public Vector2 End;
			public float Width;
			public float Depth;
		}

		public static void Crack(int seed, out Texture2D core, out Texture2D glow)
		{
			string key = "crack" + seed;
			if (cache.TryGetValue(key + "c", out core) && core != null && cache.TryGetValue(key + "g", out glow) && glow != null)
				return;
			BuildCrack(seed, out core, out glow);
			cache[key + "c"] = core;
			cache[key + "g"] = glow;
		}

		private static void BuildCrack(int seed, out Texture2D core, out Texture2D glow)
		{
			const int SIZE = 512;
			System.Random random = new(seed);
			List<Segment> segments = new();
			int mains = 9 + random.Next(0, 4);
			for (int i = 0; i < mains; i++)
			{
				float angle = (i + (float)random.NextDouble() * 0.7f) / mains * Mathf.PI * 2f;
				Grow(random, segments, Vector2.zero, angle, 0.95f, 3.4f, 0);
			}

			float half = (SIZE - 1) * 0.5f;
			float[] coreMask = new float[SIZE * SIZE];
			float[] glowMask = new float[SIZE * SIZE];
			foreach (Segment segment in segments)
			{
				float margin = segment.Width * 3f + 0.03f;
				int columnStart = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(segment.Start.x, segment.End.x) - margin) * half + half), 0, SIZE - 1);
				int columnEnd = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(segment.Start.x, segment.End.x) + margin) * half + half), 0, SIZE - 1);
				int rowStart = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(segment.Start.y, segment.End.y) - margin) * half + half), 0, SIZE - 1);
				int rowEnd = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(segment.Start.y, segment.End.y) + margin) * half + half), 0, SIZE - 1);
				for (int row = rowStart; row <= rowEnd; row++)
				for (int column = columnStart; column <= columnEnd; column++)
				{
					Vector2 point = new((column - half) / half, (row - half) / half);
					float distance = SegmentDistance(point, segment.Start, segment.End);
					float line = Mathf.Sqrt(Mathf.Clamp01(1f - distance / (segment.Width * 0.7f)));
					float spread = Gauss(distance, segment.Width * 1.6f + 0.004f);
					int index = row * SIZE + column;
					coreMask[index] = Mathf.Max(coreMask[index], line * Mathf.Lerp(0.6f, 1f, segment.Depth));
					glowMask[index] = Mathf.Max(glowMask[index], spread);
				}
			}

			Color[] corePixels = new Color[SIZE * SIZE];
			Color[] glowPixels = new Color[SIZE * SIZE];
			for (int row = 0; row < SIZE; row++)
			for (int column = 0; column < SIZE; column++)
			{
				int index = row * SIZE + column;
				float radius = Centered(column, row, SIZE, SIZE).magnitude;
				float falloff = Mathf.Clamp01((1f - radius) * 5f);
				float flicker = Mathf.Lerp(0.55f, 1f, Fbm(column / 11f, row / 11f, seed + 3, 3));
				corePixels[index] = new Color(1f, 1f, 1f, coreMask[index] * falloff);
				glowPixels[index] = new Color(1f, 1f, 1f, glowMask[index] * falloff * flicker);
			}
			core = Make("crackCore" + seed, SIZE, SIZE, corePixels);
			glow = Make("crackGlow" + seed, SIZE, SIZE, glowPixels);
		}

		// 중심에서 가지를 한 걸음씩 뻗음. 걸음마다 방향이 흔들리고 가끔 곁가지가 갈라짐
		private static void Grow(System.Random random, List<Segment> segments, Vector2 start, float angle, float length, float width, int depth)
		{
			Vector2 position = start;
			float traveled = 0f;
			float widthUv = width * 0.0048f;
			float totalLength = length * Mathf.Lerp(0.55f, 1f, (float)random.NextDouble()) * (depth == 0 ? 1f : 0.5f);
			while (traveled < totalLength)
			{
				float step = Mathf.Lerp(0.07f, 0.16f, (float)random.NextDouble());
				angle += (random.NextDouble() < 0.5 ? -1f : 1f) * Mathf.Lerp(0.12f, 0.6f, (float)random.NextDouble());
				Vector2 next = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
				float progress = traveled / Mathf.Max(0.01f, totalLength);
				segments.Add(new Segment { Start = position, End = next, Width = Mathf.Max(0.0016f, widthUv * Mathf.Lerp(1f, 0.22f, progress)), Depth = 1f - progress });
				if (depth < 3 && random.NextDouble() < 0.24)
				{
					float side = random.NextDouble() < 0.5 ? -1f : 1f;
					Grow(random, segments, next, angle + side * Mathf.Lerp(0.5f, 1.1f, (float)random.NextDouble()), (totalLength - traveled) * 0.8f, width * 0.55f, depth + 1);
				}
				position = next;
				traveled += step;
			}
		}
	}
}
