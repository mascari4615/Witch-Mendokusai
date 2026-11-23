using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 주제 물체 텍스처. 모두 도형과 잡음으로 굽는 절차적 무늬 (그림 파일 없음)
	/// - 흰색으로 구워 재질 색으로 물들이는 것: 불꽃 덩어리, 가시, 창, 광선, 십자광, 마법진, 풀잎
	/// - 색까지 굽는 것: 카드 (앞면 격자 무늬), 큰 카드, 달, 은하 부채꼴, 블랙홀, 큰 검 (도형 넷: 날, 코등이, 손잡이, 머리)
	/// </summary>
	public static partial class UltimateFxTextures
	{
		public static Texture2D FlamePuff => Get("flamePuff", 128, 128, BuildFlamePuff);
		public static Texture2D Card => Get("card", 96, 136, BuildCard);
		public static Texture2D BigCard => Get("bigCard", 256, 362, BuildBigCard);
		public static Texture2D Moon => Get("moon", 256, 256, BuildMoon);
		public static Texture2D NightFan => Get("nightFan", 512, 288, BuildNightFan);
		public static Texture2D BlackHole => Get("blackHole", 256, 256, BuildBlackHole);
		public static Texture2D DiscRing => Get("discRing", 512, 128, BuildDiscRing);
		public static Texture2D Rhombus => Get("rhombus", 512, 512, BuildRhombus);
		public static Texture2D CrossGlint => Get("crossGlint", 512, 512, BuildCrossGlint);
		public static Texture2D Spike => Get("spike", 64, 256, BuildSpike);
		public static Texture2D Spear => Get("spear", 32, 256, BuildSpear);
		public static Texture2D Beam => Get("beam", 32, 128, BuildBeam, TextureWrapMode.Repeat);
		public static Texture2D GrassBlade => Get("grassBlade", 32, 64, BuildGrassBlade);
		public static Texture2D Rune => Get("rune", 512, 512, BuildRune);
		public static Texture2D Sword => Get("sword", 128, 512, BuildSword);

		// 불꽃 덩어리: 가장자리가 잡음으로 일렁이는 원, 속이 진함
		private static void BuildFlamePuff(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float angle = Mathf.Atan2(point.y, point.x);
				float edge = 0.72f + (Fbm(Mathf.Cos(angle) * 2.2f + 4f, Mathf.Sin(angle) * 2.2f + 4f, 87, 3) - 0.5f) * 0.5f;
				float body = Mathf.Clamp01((edge - point.magnitude) / 0.25f);
				float grain = Mathf.Lerp(0.6f, 1f, Fbm(column / 9f, row / 9f, 91, 3));
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(body * body * grain * 1.3f));
			}
		}

		private static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float corner)
		{
			Vector2 offset = new(Mathf.Abs(point.x) - halfSize.x + corner, Mathf.Abs(point.y) - halfSize.y + corner);
			Vector2 outside = new(Mathf.Max(offset.x, 0f), Mathf.Max(offset.y, 0f));
			return outside.magnitude + Mathf.Min(Mathf.Max(offset.x, offset.y), 0f) - corner;
		}

		// 카드 낱장: 상아색 판, 진한 테두리, 가운데 마름모 격자
		private static void BuildCard(int width, int height, Color[] pixels)
			=> PaintCard(width, height, pixels, new Color(0.98f, 0.95f, 0.88f), new Color(0.72f, 0.08f, 0.16f), new Color(0.85f, 0.18f, 0.25f), 3.5f);

		// 큰 카드: 짙은 보라 판, 금 테두리, 동심 마름모. 낱장과 같은 꼴에 색과 무늬 밀도만 다름
		private static void BuildBigCard(int width, int height, Color[] pixels)
			=> PaintCard(width, height, pixels, new Color(0.2f, 0.07f, 0.3f), new Color(1f, 0.82f, 0.35f), new Color(0.95f, 0.3f, 0.55f), 4f);

		private static void PaintCard(int width, int height, Color[] pixels, Color face, Color border, Color pattern, float latticeCount)
		{
			float aspect = width / (float)height;
			Vector2 halfSize = new(aspect * 0.96f, 0.96f);
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				point.x *= aspect;
				float outline = RoundedRectDistance(point, halfSize, 0.12f);
				float alpha = Mathf.Clamp01(-outline * height * 0.5f);
				Color color = face;
				float borderBand = Mathf.Clamp01(1f - Mathf.Abs(outline + 0.06f) * height * 0.05f);
				float inner = RoundedRectDistance(point, halfSize - new Vector2(0.16f, 0.16f), 0.06f);
				if (inner < 0f)
				{
					float diamond = Mathf.Abs(point.x) + Mathf.Abs(point.y);
					float lattice = Mathf.Abs(Mathf.Sin((point.x + point.y) * latticeCount * Mathf.PI)) * Mathf.Abs(Mathf.Sin((point.x - point.y) * latticeCount * Mathf.PI));
					float rings = Mathf.Abs(Mathf.Sin(diamond * latticeCount * 2f * Mathf.PI));
					float mark = Mathf.Max(Mathf.SmoothStep(0.55f, 0.9f, lattice), Mathf.SmoothStep(0.9f, 1f, rings) * 0.6f);
					color = Color.Lerp(face, pattern, mark);
				}
				color = Color.Lerp(color, border, Mathf.Max(borderBand, Mathf.Clamp01(1f - Mathf.Abs(inner) * height * 0.08f)));
				color.a = alpha;
				pixels[row * width + column] = color;
			}
		}

		// 달: 왼쪽 위에서 빛을 받는 원판, 잡음 얼룩과 분화구, 오른쪽 아래 어두운 쪽
		private static void BuildMoon(int width, int height, Color[] pixels)
		{
			Vector3 light = new Vector3(-0.55f, 0.6f, 0.58f).normalized;
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height) / 0.96f;
				float radiusSquared = point.sqrMagnitude;
				if (radiusSquared >= 1f)
				{
					float glow = Mathf.Clamp01(1f - (Mathf.Sqrt(radiusSquared) - 1f) / 0.04f);
					pixels[row * width + column] = new Color(0.85f, 0.9f, 1f, glow * 0.6f);
					continue;
				}
				Vector3 normal = new(point.x, point.y, Mathf.Sqrt(1f - radiusSquared));
				float lit = Mathf.Clamp01(Vector3.Dot(normal, light) * 1.15f + 0.08f);
				float patches = Fbm(column / 40f, row / 40f, 131, 4);
				float craters = 0f;
				for (int i = 0; i < 14; i++)
				{
					Vector2 crater = new(Hash(i, 1, 141) * 1.6f - 0.8f, Hash(i, 2, 141) * 1.6f - 0.8f);
					float craterRadius = Mathf.Lerp(0.05f, 0.17f, Hash(i, 3, 141));
					float distance = (point - crater).magnitude / craterRadius;
					craters += Mathf.Clamp01(1f - distance) * 0.35f - Gauss(distance - 1f, 0.12f) * 0.12f;
				}
				float albedo = Mathf.Clamp01(0.82f - patches * 0.35f - craters);
				float shade = albedo * lit;
				Color color = Color.Lerp(new Color(0.12f, 0.14f, 0.26f), new Color(0.95f, 0.96f, 1f), shade);
				float rim = Mathf.Pow(Mathf.Clamp01(Mathf.Sqrt(radiusSquared) - 0.9f) * 10f, 2f) * lit;
				pixels[row * width + column] = new Color(color.r + rim * 0.3f, color.g + rim * 0.3f, color.b + rim * 0.3f, 1f);
			}
		}

		// 은하 부채꼴: 아래 가운데를 꼭지로 한 부채꼴 창, 안은 보라 청색 성운과 별, 가장자리 분홍 테
		private static void BuildNightFan(int width, int height, Color[] pixels)
		{
			const float INNER = 0.18f;
			const float SPREAD_DEGREES = 72f;
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = new(column / (width - 1f) * 2f - 1f, row / (height - 1f) * 1.125f);
				float radius = point.magnitude;
				float angleDegrees = Mathf.Abs(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg);
				float sideEdge = (SPREAD_DEGREES - angleDegrees) * Mathf.Deg2Rad * radius;
				float outerEdge = 1f - radius;
				float innerEdge = radius - INNER;
				float edge = Mathf.Min(sideEdge, Mathf.Min(outerEdge, innerEdge));
				float alpha = Mathf.Clamp01(edge * height * 0.5f);
				float nebula = Fbm(column / 60f, row / 60f, 151, 5);
				float swirl = Fbm(column / 25f + nebula * 3f, row / 25f, 157, 4);
				Color color = Color.Lerp(new Color(0.06f, 0.04f, 0.2f), new Color(0.45f, 0.2f, 0.7f), Mathf.SmoothStep(0.35f, 0.75f, nebula));
				color = Color.Lerp(color, new Color(0.35f, 0.6f, 1f), Mathf.SmoothStep(0.6f, 0.85f, swirl) * 0.7f);
				float star = Hash(column, row, 163) > 0.992f ? Hash(column, row, 167) : 0f;
				color += new Color(star, star, star, 0f);
				float rim = Mathf.Clamp01(1f - edge * height * 0.08f);
				color = Color.Lerp(color, new Color(1f, 0.55f, 0.75f), rim);
				color.a = alpha;
				pixels[row * width + column] = color;
			}
		}

		// 블랙홀: 검은 원판, 청록 테, 바깥 옅은 빛무리
		private static void BuildBlackHole(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = point.magnitude;
				float disc = Mathf.Clamp01((0.56f - radius) * height * 0.5f);
				float angle = Mathf.Atan2(point.y, point.x);
				float flicker = Mathf.Lerp(0.7f, 1f, Fbm(Mathf.Cos(angle) * 4f + 9f, Mathf.Sin(angle) * 4f + 9f, 171, 3));
				float rim = Gauss(radius - 0.58f, 0.025f) * flicker;
				float halo = Mathf.Exp(-Mathf.Max(0f, radius - 0.58f) / 0.12f) * 0.55f * (radius > 0.56f ? 1f : 0f) * flicker;
				Color glow = new Color(0.4f, 1f, 1f) * (rim + halo) + new Color(1f, 1f, 1f) * rim * 0.6f;
				float alpha = Mathf.Clamp01(disc + rim + halo) * Mathf.Clamp01((1f - radius) * 8f);
				Color color = new(glow.r * (1f - disc), glow.g * (1f - disc), glow.b * (1f - disc), alpha);
				pixels[row * width + column] = color;
			}
		}

		// 원반 고리: 블랙홀을 가로지르는 납작한 타원 고리
		private static void BuildDiscRing(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = new Vector2(point.x, point.y * 4f).magnitude;
				float band = Gauss(radius - 0.82f, 0.05f) + Gauss(radius - 0.82f, 0.15f) * 0.35f;
				float grain = Mathf.Lerp(0.6f, 1f, Fbm(Mathf.Atan2(point.y * 4f, point.x) * 6f + 20f, radius * 8f, 173, 3));
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * grain) * Mathf.Clamp01((1f - Mathf.Abs(point.x)) * 20f));
			}
		}

		// 마름모 마법진: 겹 마름모, 원, 가운데 고리, 사이 눈금
		private static void BuildRhombus(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float diamond = Mathf.Abs(point.x) + Mathf.Abs(point.y);
				float radius = point.magnitude;
				float alpha = 0f;
				alpha += Gauss(diamond - 0.96f, 0.01f);
				alpha += Gauss(diamond - 0.88f, 0.006f) * 0.8f;
				alpha += Gauss(diamond - 0.62f, 0.006f) * 0.7f;
				alpha += Gauss(radius - 0.62f, 0.006f) * 0.8f;
				alpha += Gauss(radius - 0.2f, 0.012f);
				alpha += Gauss(radius - 0.12f, 0.005f) * 0.7f;
				// 축 선과 대각 십자
				alpha += Gauss(Mathf.Abs(point.x), 0.004f) * (diamond < 0.96f ? 0.6f : 0f);
				alpha += Gauss(Mathf.Abs(point.y), 0.004f) * (diamond < 0.96f ? 0.6f : 0f);
				// 두 마름모 사이 눈금
				if (diamond > 0.88f && diamond < 0.96f)
				{
					float along = (point.x - point.y) * 18f;
					alpha += Gauss(along - Mathf.Round(along), 0.08f) * 0.8f;
				}
				// 안쪽 원과 마름모 사이 작은 점 무늬
				if (radius > 0.25f && diamond < 0.6f)
				{
					float cellX = point.x * 14f;
					float cellY = point.y * 14f;
					float dot = Gauss(new Vector2(cellX - Mathf.Round(cellX), cellY - Mathf.Round(cellY)).magnitude, 0.12f);
					alpha += dot * 0.35f * Hash(Mathf.RoundToInt(cellX), Mathf.RoundToInt(cellY), 181);
				}
				float fill = Mathf.Clamp01(0.96f - diamond) * 0.08f;
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha + fill) * Mathf.Clamp01((1f - diamond) * 30f));
			}
		}

		// 십자광: 가로세로 긴 빛살 넷, 대각 짧은 빛살 넷, 가운데 빛
		private static void BuildCrossGlint(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = point.magnitude;
				float horizontal = Mathf.Exp(-Mathf.Abs(point.y) / (0.006f + 0.02f * (1f - Mathf.Abs(point.x)))) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(point.x)), 1.5f);
				float vertical = Mathf.Exp(-Mathf.Abs(point.x) / (0.006f + 0.02f * (1f - Mathf.Abs(point.y)))) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(point.y)), 1.5f);
				Vector2 rotated = new(point.x + point.y, point.x - point.y);
				float diagonal = (Mathf.Exp(-Mathf.Abs(rotated.y) / 0.01f) + Mathf.Exp(-Mathf.Abs(rotated.x) / 0.01f)) * Mathf.Pow(Mathf.Clamp01(1f - radius * 2.2f), 2f) * 0.6f;
				float core = Mathf.Pow(Mathf.Clamp01(1f - radius * 6f), 2f) + Mathf.Pow(Mathf.Clamp01(1f - radius * 1.6f), 4f) * 0.3f;
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(horizontal + vertical + diagonal + core));
			}
		}

		// 결정 가시: 아래가 넓고 위가 뾰족한 삼각, 가운데 면이 밝은 두 면 음영
		private static void BuildSpike(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float across = column / (width - 1f) * 2f - 1f;
				float along = row / (height - 1f);
				float halfWidth = Mathf.Lerp(0.95f, 0f, along);
				float edge = (halfWidth - Mathf.Abs(across)) * width * 0.5f;
				float alpha = Mathf.Clamp01(edge);
				float facet = across < 0f ? 1f : 0.62f;
				float ridge = Gauss(across, 0.06f) * 0.4f;
				float shade = Mathf.Clamp01(facet * Mathf.Lerp(0.7f, 1f, along) + ridge);
				float rim = Mathf.Clamp01(1f - edge * 0.5f) * 0.5f;
				shade = Mathf.Clamp01(shade + rim);
				pixels[row * width + column] = new Color(shade, shade, shade, alpha);
			}
		}

		// 창: 가는 자루와 뾰족한 날 끝, 뒤로 옅은 꼬리
		private static void BuildSpear(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float across = Mathf.Abs(column / (width - 1f) * 2f - 1f);
				float along = row / (height - 1f);
				float shaft = Mathf.Clamp01((0.16f - across) * width * 0.5f) * Mathf.SmoothStep(0f, 0.5f, along);
				float headWidth = along > 0.78f ? Mathf.Lerp(0.85f, 0f, (along - 0.78f) / 0.22f) : 0f;
				float head = Mathf.Clamp01((headWidth - across) * width * 0.5f);
				float tail = Gauss(across, 0.25f) * Mathf.SmoothStep(0f, 0.6f, along) * 0.25f;
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(shaft, head) + tail));
			}
		}

		// 광선 단면: 세로가 폭 방향. 흰 심, 노란 몸통, 가장자리 무지개 번짐. 가로 (길이 방향) 는 잔물결
		private static void BuildBeam(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float across = row / (height - 1f) * 2f - 1f;
				float distance = Mathf.Abs(across);
				float ripple = Mathf.Lerp(0.85f, 1f, Fbm(column / 6f, row / 30f, 191, 2));
				float core = Gauss(distance, 0.22f);
				float body = Gauss(distance, 0.55f);
				float hue = Mathf.Clamp01((distance - 0.62f) / 0.3f);
				Color fringe = Color.HSVToRGB(Mathf.Lerp(0.0f, 0.75f, hue), 0.8f, 1f);
				float fringeAmount = Gauss(distance - 0.78f, 0.12f);
				Color color = Color.Lerp(new Color(1f, 0.88f, 0.35f), Color.white, core);
				color = Color.Lerp(color, fringe, fringeAmount * (1f - core));
				color.a = Mathf.Clamp01((body + fringeAmount * 0.6f) * ripple);
				pixels[row * width + column] = color;
			}
		}

		// 풀잎: 아래가 넓고 위가 휘어 뾰족한 잎 하나
		private static void BuildGrassBlade(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float along = row / (height - 1f);
				float bend = along * along * 0.35f;
				float across = column / (width - 1f) * 2f - 1f - bend;
				float halfWidth = Mathf.Lerp(0.5f, 0.02f, along);
				float alpha = Mathf.Clamp01((halfWidth - Mathf.Abs(across)) * width * 0.5f);
				float shade = Mathf.Lerp(0.7f, 1f, along);
				pixels[row * width + column] = new Color(shade, shade, shade, alpha);
			}
		}

		// 지면 룬: 겹동심원과 사이 룬 획, 육각별
		private static void BuildRune(int width, int height, Color[] pixels)
		{
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				Vector2 point = Centered(column, row, width, height);
				float radius = point.magnitude;
				float angle = Mathf.Atan2(point.y, point.x);
				float alpha = Gauss(radius - 0.94f, 0.012f) + Gauss(radius - 0.84f, 0.008f) * 0.8f + Gauss(radius - 0.5f, 0.01f) * 0.7f;
				if (radius > 0.84f && radius < 0.94f)
				{
					float slotPosition = (angle / (Mathf.PI * 2f) + 0.5f) * 36f;
					int slot = Mathf.FloorToInt(slotPosition);
					float local = slotPosition - slot;
					float bar = Gauss(local - 0.5f, 0.07f) * (Hash(slot, 1, 7) > 0.35f ? 1f : 0f);
					alpha += bar * 0.9f;
				}
				alpha += StarLine(point, 6, 2, 0.8f) * 0.85f;
				float grain = Mathf.Lerp(0.6f, 1f, Fbm(column / 7f, row / 7f, 61, 2));
				pixels[row * width + column] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * grain * Mathf.Clamp01((1f - radius) * 20f));
			}
		}

		private static float StarLine(Vector2 point, int points, int step, float radius)
		{
			float best = 0f;
			for (int i = 0; i < points; i++)
			{
				float startAngle = i * Mathf.PI * 2f / points + Mathf.PI * 0.5f;
				float endAngle = (i + step) * Mathf.PI * 2f / points + Mathf.PI * 0.5f;
				Vector2 start = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * radius;
				Vector2 end = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius;
				best = Mathf.Max(best, Gauss(SegmentDistance(point, start, end), 0.008f));
			}
			return best;
		}

		// 큰 검: 아래가 칼끝. 은빛 날 (가운데 홈이 밝음), 금 코등이, 짙은 손잡이, 금 머리. 아래쪽 5% 는 땅에 박히는 칼끝
		private static void BuildSword(int width, int height, Color[] pixels)
		{
			Color silver = new(0.82f, 0.88f, 0.98f);
			Color steelEdge = new(0.45f, 0.52f, 0.68f);
			Color gold = new(1f, 0.78f, 0.3f);
			Color grip = new(0.22f, 0.14f, 0.2f);
			for (int row = 0; row < height; row++)
			for (int column = 0; column < width; column++)
			{
				float across = column / (width - 1f) * 2f - 1f;
				float along = row / (height - 1f);
				float alpha = 0f;
				Color color = silver;
				float bladeHalf = along < 0.12f ? Mathf.Lerp(0f, 0.36f, along / 0.12f) : 0.36f;
				if (along < 0.7f && Mathf.Abs(across) < bladeHalf)
				{
					alpha = Mathf.Clamp01((bladeHalf - Mathf.Abs(across)) * width * 0.5f);
					float fuller = Gauss(across, 0.07f);
					color = Color.Lerp(steelEdge, silver, Mathf.Clamp01(1f - Mathf.Abs(across) / Mathf.Max(0.01f, bladeHalf) + fuller * 0.6f));
					color = Color.Lerp(color, Color.white, fuller * 0.7f);
				}
				float guard = Mathf.Abs(along - 0.72f);
				if (guard < 0.025f && Mathf.Abs(across) < 0.95f - guard * 8f)
				{
					alpha = 1f;
					color = Color.Lerp(gold, Color.white, Gauss(along - 0.73f, 0.008f) * 0.6f);
				}
				if (along > 0.745f && along < 0.92f && Mathf.Abs(across) < 0.14f)
				{
					alpha = 1f;
					float wrap = Mathf.Abs(Mathf.Sin(along * 120f)) * 0.3f;
					color = Color.Lerp(grip, Color.black, wrap);
				}
				Vector2 pommel = new(across * 0.5f, (along - 0.95f) * height / width);
				if (pommel.magnitude < 0.09f)
				{
					alpha = 1f;
					color = Color.Lerp(gold, Color.white, Mathf.Clamp01(0.09f - pommel.magnitude) * 4f);
				}
				color.a = alpha;
				pixels[row * width + column] = color;
			}
		}
	}
}
