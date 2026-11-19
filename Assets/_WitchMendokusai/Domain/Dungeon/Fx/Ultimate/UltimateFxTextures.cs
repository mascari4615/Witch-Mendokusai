using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 연출 텍스처를 코드로 한 번 구워 재사용. 부드러운 빛, 불꽃, 연기, 파편, 고리, 속도선, 빛기둥, 지면 균열 세 장
	/// - 도형 한 가지를 늘려 쓰지 않고 잡음 (fbm) 으로 가장자리를 흔들어 입자마다 모양이 다르게
	/// - 균열은 가지를 재귀로 뻗어 선분을 만들고 선분 거리로 핵 (하얀 속), 번짐 (붉은 빛), 그을음 (검은 자국) 세 장에 그림
	/// - 에셋 파일이 아니라 첫 사용 때 굽는다. 같은 칸에 사용자 텍스처를 끼우면 그것을 우선 (UltimateFx 의 override 칸)
	/// </summary>
	public static class UltimateFxTextures
	{
		private static readonly Dictionary<string, Texture2D> cache = new();

		public static Texture2D Soft => Get("soft", 128, BuildSoft);
		public static Texture2D Spark => Get("spark", 128, BuildSpark);
		public static Texture2D Smoke => Get("smoke", 256, BuildSmoke);
		public static Texture2D Ring => Get("ring", 512, BuildRing);
		public static Texture2D Streaks => Get("streaks", 512, BuildStreaks);
		public static Texture2D Pillar => Get("pillar", 256, BuildPillar);
		public static Texture2D Vignette => Get("vignette", 256, BuildVignette);
		public static Texture2D Rune => Get("rune", 512, BuildRune);
		public static Texture2D White => Get("white", 4, (size, pixels) => { for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white; });

		public static Texture2D[] Shards
		{
			get
			{
				Texture2D[] result = new Texture2D[4];
				for (int i = 0; i < result.Length; i++)
				{
					int seed = i * 977 + 31;
					result[i] = Get("shard" + i, 64, (size, pixels) => BuildShard(size, pixels, seed));
				}
				return result;
			}
		}

		/// <summary> 균열 세 장: Core 하얀 속 선, Glow 붉은 번짐, Scorch 검은 그을음. 시드마다 다른 모양 </summary>
		public static void Crack(int seed, out Texture2D core, out Texture2D glow, out Texture2D scorch)
		{
			string key = "crack" + seed;
			if (cache.TryGetValue(key + "c", out core) && core != null && cache.TryGetValue(key + "g", out glow) && glow != null && cache.TryGetValue(key + "s", out scorch) && scorch != null)
				return;
			BuildCrack(seed, out core, out glow, out scorch);
			cache[key + "c"] = core;
			cache[key + "g"] = glow;
			cache[key + "s"] = scorch;
		}

		private static Texture2D Get(string key, int size, System.Action<int, Color[]> fill)
		{
			if (cache.TryGetValue(key, out Texture2D texture) && texture != null)
				return texture;
			Color[] pixels = new Color[size * size];
			fill(size, pixels);
			texture = Make(key, size, size, pixels);
			cache[key] = texture;
			return texture;
		}

		private static Texture2D Make(string name, int width, int height, Color[] pixels)
		{
			Texture2D texture = new(width, height, TextureFormat.RGBA32, true) { name = "UltimateFx_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
			texture.SetPixels(pixels);
			texture.Apply(true);
			return texture;
		}

		// ---- 잡음 ----
		private static float Hash(int x, int y, int seed)
		{
			unchecked
			{
				int h = x * 374761393 + y * 668265263 + seed * 1274126177;
				h = (h ^ (h >> 13)) * 1274126177;
				h ^= h >> 16;
				return (h & 0xffff) / 65535f;
			}
		}

		private static float ValueNoise(float x, float y, int seed)
		{
			int ix = Mathf.FloorToInt(x);
			int iy = Mathf.FloorToInt(y);
			float fx = x - ix;
			float fy = y - iy;
			fx = fx * fx * (3f - 2f * fx);
			fy = fy * fy * (3f - 2f * fy);
			float a = Hash(ix, iy, seed);
			float b = Hash(ix + 1, iy, seed);
			float c = Hash(ix, iy + 1, seed);
			float d = Hash(ix + 1, iy + 1, seed);
			return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
		}

		private static float Fbm(float x, float y, int seed, int octaves = 4)
		{
			float sum = 0f;
			float amplitude = 0.5f;
			float norm = 0f;
			for (int i = 0; i < octaves; i++)
			{
				sum += ValueNoise(x, y, seed + i * 17) * amplitude;
				norm += amplitude;
				x *= 2f;
				y *= 2f;
				amplitude *= 0.5f;
			}
			return sum / norm;
		}

		// ---- 모양 ----
		private static void BuildSoft(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float r = Mathf.Sqrt(Sq((x - half) / half) + Sq((y - half) / half));
				float a = Mathf.Clamp01(1f - r);
				a = a * a * (3f - 2f * a);
				pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
			}
		}

		private static void BuildSpark(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float u = (x - half) / half;
				float v = (y - half) / half;
				float r = Mathf.Sqrt(u * u + v * v);
				float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.2f), 2f);
				float h = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 6f) * Mathf.Exp(-Mathf.Abs(v) * 28f);
				float vert = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 6f) * Mathf.Exp(-Mathf.Abs(u) * 28f);
				float diag = Mathf.Exp(-Mathf.Abs(Mathf.Abs(u) - Mathf.Abs(v)) * 40f) * Mathf.Pow(Mathf.Clamp01(1f - r), 4f) * 0.5f;
				float halo = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.25f;
				pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(core + h + vert + diag + halo));
			}
		}

		private static void BuildSmoke(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float u = (x - half) / half;
				float v = (y - half) / half;
				float r = Mathf.Sqrt(u * u + v * v);
				float n = Fbm(x / 28f, y / 28f, 5, 5);
				float edge = Mathf.Clamp01((1f - r) * 1.6f);
				float a = Mathf.Clamp01((n * 1.5f - 0.3f) * edge * edge);
				float shade = Mathf.Lerp(0.55f, 1f, Fbm(x / 20f + 9f, y / 20f, 11, 3));
				pixels[y * size + x] = new Color(shade, shade, shade, a);
			}
		}

		private static void BuildRing(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float u = (x - half) / half;
				float v = (y - half) / half;
				float r = Mathf.Sqrt(u * u + v * v);
				float angle = Mathf.Atan2(v, u);
				// 가장자리가 들쭉날쭉한 불규칙 고리. 안쪽은 길게 끌리는 꼬리
				float wobble = (Fbm(Mathf.Cos(angle) * 3f + 5f, Mathf.Sin(angle) * 3f + 5f, 3, 3) - 0.5f) * 0.09f;
				float d = r - (0.86f + wobble);
				float front = Mathf.Exp(-Sq(d / 0.028f));
				float tail = d < 0f ? Mathf.Exp(d / 0.16f) * 0.55f : 0f;
				float outer = d > 0f ? Mathf.Exp(-d / 0.05f) * 0.5f : 0f;
				float grain = Mathf.Lerp(0.75f, 1f, Fbm(x / 9f, y / 9f, 21, 3));
				float edgeCut = Mathf.Clamp01((1f - r) * 12f);
				pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((front + tail + outer) * grain) * edgeCut);
			}
		}

		private static void BuildStreaks(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			const int LINES = 150;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float u = (x - half) / half;
				float v = (y - half) / half;
				float r = Mathf.Sqrt(u * u + v * v);
				float angle = (Mathf.Atan2(v, u) / (Mathf.PI * 2f) + 0.5f) * LINES;
				int index = Mathf.FloorToInt(angle);
				float frac = angle - index;
				float strength = Hash(index, 3, 71);
				float length = Hash(index, 9, 13);
				float thick = Mathf.Lerp(0.04f, 0.22f, Hash(index, 5, 29));
				float line = Mathf.Clamp01(1f - Mathf.Abs(frac - 0.5f) / thick);
				float inner = Mathf.Lerp(0.38f, 0.78f, length);
				float radial = Mathf.Clamp01((r - inner) / 0.18f) * Mathf.Clamp01((1.02f - r) / 0.15f);
				pixels[y * size + x] = new Color(1f, 1f, 1f, line * line * radial * Mathf.Lerp(0.3f, 1f, strength));
			}
		}

		private static void BuildPillar(int size, Color[] pixels)
		{
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float u = x / (size - 1f) * 2f - 1f;
				float v = y / (size - 1f);
				float wobble = (Fbm(v * 6f, 2f, 41, 3) - 0.5f) * 0.18f;
				float width = Mathf.Lerp(0.5f, 0.3f, v) + wobble;
				float core = Mathf.Exp(-Sq(u / (width * 0.28f)));
				float body = Mathf.Exp(-Sq(u / width)) * 0.55f;
				float streak = Mathf.Lerp(0.55f, 1f, Fbm(u * 14f + 3f, v * 3f, 53, 3));
				float fadeTop = Mathf.Clamp01((1f - v) * 3f);
				float fadeBottom = Mathf.Clamp01(v * 10f);
				pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((core + body) * streak) * fadeTop * fadeBottom);
			}
		}

		private static void BuildVignette(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float r = Mathf.Sqrt(Sq((x - half) / half) + Sq((y - half) / half)) / 1.4142f;
				float a = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1f, r)), 1.7f);
				pixels[y * size + x] = new Color(1f, 1f, 1f, a);
			}
		}

		// 지면에 깔리는 룬 무늬. 겹동심원, 방사 선, 기호 획
		private static void BuildRune(int size, Color[] pixels)
		{
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float u = (x - half) / half;
				float v = (y - half) / half;
				float r = Mathf.Sqrt(u * u + v * v);
				float angle = Mathf.Atan2(v, u);
				float a = 0f;
				a += Mathf.Exp(-Sq((r - 0.94f) / 0.012f));
				a += Mathf.Exp(-Sq((r - 0.84f) / 0.008f)) * 0.8f;
				a += Mathf.Exp(-Sq((r - 0.5f) / 0.01f)) * 0.7f;
				// 두 번째 고리 사이 룬 획
				if (r > 0.84f && r < 0.94f)
				{
					int slot = Mathf.FloorToInt((angle / (Mathf.PI * 2f) + 0.5f) * 36f);
					float local = ((angle / (Mathf.PI * 2f) + 0.5f) * 36f) - slot;
					float bar = Mathf.Exp(-Sq((local - 0.5f) / 0.07f)) * (Hash(slot, 1, 7) > 0.35f ? 1f : 0f);
					float cross = Mathf.Exp(-Sq((r - Mathf.Lerp(0.855f, 0.925f, Hash(slot, 2, 8))) / 0.006f)) * Mathf.Exp(-Sq((local - 0.5f) / 0.22f));
					a += (bar + cross) * 0.9f;
				}
				// 육각별
				a += StarLine(u, v, 6, 2, 0.8f) * 0.85f;
				a += StarLine(u, v, 3, 1, 0.5f) * 0.7f;
				float grain = Mathf.Lerp(0.6f, 1f, Fbm(x / 7f, y / 7f, 61, 2));
				pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a) * grain * Mathf.Clamp01((1f - r) * 20f));
			}
		}

		private static float StarLine(float u, float v, int points, int step, float radius)
		{
			float best = 0f;
			for (int i = 0; i < points; i++)
			{
				float a0 = i * Mathf.PI * 2f / points + Mathf.PI * 0.5f;
				float a1 = (i + step) * Mathf.PI * 2f / points + Mathf.PI * 0.5f;
				Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
				Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
				float d = SegmentDistance(new Vector2(u, v), p0, p1);
				best = Mathf.Max(best, Mathf.Exp(-Sq(d / 0.008f)));
			}
			return best;
		}

		private static void BuildShard(int size, Color[] pixels, int seed)
		{
			// 꼭짓점 일곱 개의 불규칙한 다각형. 왼쪽 위의 빛을 받는 음영과 밝은 가장자리
			const int VERTS = 7;
			Vector2[] verts = new Vector2[VERTS];
			for (int i = 0; i < VERTS; i++)
			{
				float angle = (i + Hash(i, 1, seed) * 0.6f) / VERTS * Mathf.PI * 2f;
				float radius = Mathf.Lerp(0.55f, 0.92f, Hash(i, 2, seed));
				verts[i] = new Vector2(Mathf.Cos(angle) * radius * 1.0f, Mathf.Sin(angle) * radius * 0.8f);
			}
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				Vector2 p = new((x - half) / half, (y - half) / half);
				float signed = SignedPolygonDistance(p, verts);
				float alpha = Mathf.Clamp01(-signed * half * 0.5f + 0.5f);
				float light = Mathf.Clamp01(0.55f + (-p.x * 0.35f + p.y * 0.35f) + (Fbm(x / 6f, y / 6f, seed, 3) - 0.5f) * 0.5f);
				float rim = Mathf.Clamp01(1f - Mathf.Abs(signed) * 7f) * 0.35f;
				float shade = Mathf.Clamp01(light + rim);
				pixels[y * size + x] = new Color(shade, shade, shade, alpha);
			}
		}

		private static float SignedPolygonDistance(Vector2 p, Vector2[] verts)
		{
			float minDist = float.MaxValue;
			bool inside = false;
			for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
			{
				minDist = Mathf.Min(minDist, SegmentDistance(p, verts[i], verts[j]));
				if ((verts[i].y > p.y) != (verts[j].y > p.y) && p.x < (verts[j].x - verts[i].x) * (p.y - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x)
					inside = !inside;
			}
			return inside ? -minDist : minDist;
		}

		private struct Segment
		{
			public Vector2 A;
			public Vector2 B;
			public float Width;
			public float Depth;
		}

		private static void BuildCrack(int seed, out Texture2D core, out Texture2D glow, out Texture2D scorch)
		{
			const int SIZE = 512;
			System.Random random = new(seed);
			List<Segment> segments = new();
			int mains = 9 + random.Next(0, 4);
			for (int i = 0; i < mains; i++)
			{
				float angle = (i + (float)random.NextDouble() * 0.7f) / mains * Mathf.PI * 2f;
				Grow(random, segments, Vector2.zero, angle, 0.95f, 4.2f, 0);
			}

			float half = (SIZE - 1) * 0.5f;
			Color[] corePixels = new Color[SIZE * SIZE];
			Color[] glowPixels = new Color[SIZE * SIZE];
			Color[] scorchPixels = new Color[SIZE * SIZE];
			float[] coreMask = new float[SIZE * SIZE];
			float[] glowMask = new float[SIZE * SIZE];
			float[] rimMask = new float[SIZE * SIZE];
			foreach (Segment segment in segments)
			{
				float margin = (segment.Width * 4.2f + 0.05f);
				float minX = Mathf.Min(segment.A.x, segment.B.x) - margin;
				float maxX = Mathf.Max(segment.A.x, segment.B.x) + margin;
				float minY = Mathf.Min(segment.A.y, segment.B.y) - margin;
				float maxY = Mathf.Max(segment.A.y, segment.B.y) + margin;
				int x0 = Mathf.Clamp(Mathf.FloorToInt(minX * half + half), 0, SIZE - 1);
				int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * half + half), 0, SIZE - 1);
				int y0 = Mathf.Clamp(Mathf.FloorToInt(minY * half + half), 0, SIZE - 1);
				int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY * half + half), 0, SIZE - 1);
				for (int y = y0; y <= y1; y++)
				for (int x = x0; x <= x1; x++)
				{
					Vector2 p = new((x - half) / half, (y - half) / half);
					float d = SegmentDistance(p, segment.A, segment.B);
					float c = Mathf.Clamp01(1f - d / (segment.Width * 0.7f));
					c = Mathf.Sqrt(c);
					float g = Mathf.Exp(-Sq(d / (segment.Width * 1.25f + 0.005f)));
					float rim = Mathf.Exp(-Sq(d / (segment.Width * 2.1f + 0.006f)));
					int index = y * SIZE + x;
					rimMask[index] = Mathf.Max(rimMask[index], rim);
					coreMask[index] = Mathf.Max(coreMask[index], c * Mathf.Lerp(0.6f, 1f, segment.Depth));
					glowMask[index] = Mathf.Max(glowMask[index], g);
				}
			}

			for (int y = 0; y < SIZE; y++)
			for (int x = 0; x < SIZE; x++)
			{
				int index = y * SIZE + x;
				float u = (x - half) / half;
				float v = (y - half) / half;
				float r = Mathf.Sqrt(u * u + v * v);
				float falloff = Mathf.Clamp01((1f - r) * 5f);
				float flicker = Mathf.Lerp(0.55f, 1f, Fbm(x / 11f, y / 11f, seed + 3, 3));
				corePixels[index] = new Color(1f, 1f, 1f, coreMask[index] * falloff);
				glowPixels[index] = new Color(1f, 1f, 1f, glowMask[index] * falloff * flicker);
				// 그을음: 균열 둘레 넓은 번짐 + 중심에서 바깥으로 얇아지는 원판, 잡음으로 얼룩
				float blotch = Mathf.Clamp01(Fbm(x / 34f, y / 34f, seed + 7, 4) * 1.6f - 0.1f);
				float soot = Mathf.Max(rimMask[index] * 0.9f, Mathf.Exp(-r * r * 2.6f) * 0.6f * blotch);
				scorchPixels[index] = new Color(0.015f, 0.01f, 0.008f, Mathf.Clamp01(soot) * Mathf.Clamp01((1f - r) * 6f));
			}

			core = Make("crackCore" + seed, SIZE, SIZE, corePixels);
			glow = Make("crackGlow" + seed, SIZE, SIZE, glowPixels);
			scorch = Make("crackScorch" + seed, SIZE, SIZE, scorchPixels);
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
				// 곧게 뻗다가 모서리에서 꺾임
				angle += (random.NextDouble() < 0.5 ? -1f : 1f) * Mathf.Lerp(0.12f, 0.6f, (float)random.NextDouble());
				Vector2 next = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
				float t = traveled / Mathf.Max(0.01f, totalLength);
				segments.Add(new Segment { A = position, B = next, Width = Mathf.Max(0.0016f, widthUv * Mathf.Lerp(1f, 0.22f, t)), Depth = 1f - t });
				if (depth < 3 && random.NextDouble() < 0.24)
				{
					float side = random.NextDouble() < 0.5 ? -1f : 1f;
					Grow(random, segments, next, angle + side * Mathf.Lerp(0.5f, 1.1f, (float)random.NextDouble()), (totalLength - traveled) * 0.8f, width * 0.55f, depth + 1);
				}
				position = next;
				traveled += step;
			}
		}

		private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float length2 = ab.sqrMagnitude;
			float t = length2 < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / length2);
			return (p - (a + ab * t)).magnitude;
		}

		private static float Sq(float value) => value * value;
	}
}
