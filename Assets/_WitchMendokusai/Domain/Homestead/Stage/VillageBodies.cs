using UnityEngine;

namespace WitchMendokusai
{
	// 마을 건물과 소품의 생김새. 흰 벽, 보라 틀, 금 선은 연구소 관제홀과 같은 재질
	public static class VillageBodies
	{
		private static readonly Color WHITE = Color.white;
		private static readonly Color ROOF_PURPLE = new(0.60f, 0.50f, 0.72f);
		private static readonly Color ROOF_ROSE = new(0.86f, 0.56f, 0.50f);
		private static readonly Color ROOF_TEAL = new(0.46f, 0.66f, 0.68f);
		private static readonly Color ROOF_HONEY = new(0.90f, 0.74f, 0.46f);
		private static readonly Color ROOF_SLATE = new(0.52f, 0.54f, 0.62f);
		private static readonly Color CANVAS = new(0.95f, 0.88f, 0.74f);
		private static readonly Color SOIL_DARK = new(0.72f, 0.58f, 0.48f);

		private static HomesteadArt Art => HomesteadArt.Instance;

		// 벽, 기둥, 문, 창, 박공 지붕. 문은 -z 쪽
		private static VillageKit House(float width, float depth, float height, Color roof)
		{
			VillageKit kit = new();
			kit.Box(Art.Stone, WHITE, new Vector3(0f, -0.2f, 0f), new Vector3(width + 0.4f, 0.45f, depth + 0.4f));
			kit.Box(Art.Plaster, WHITE, new Vector3(0f, 0.2f, 0f), new Vector3(width, height, depth));
			for (int sideX = -1; sideX <= 1; sideX += 2)
			{
				for (int sideZ = -1; sideZ <= 1; sideZ += 2)
					kit.Box(Art.Frame, WHITE, new Vector3(sideX * width * 0.5f, 0.2f, sideZ * depth * 0.5f), new Vector3(0.24f, height, 0.24f));
			}
			for (int sideZ = -1; sideZ <= 1; sideZ += 2)
				kit.Box(Art.Frame, WHITE, new Vector3(0f, height + 0.1f, sideZ * depth * 0.5f), new Vector3(width + 0.2f, 0.2f, 0.22f));
			Door(kit, 0f, -depth * 0.5f);
			Window(kit, -width * 0.32f, -depth * 0.5f, height * 0.5f);
			Window(kit, width * 0.32f, -depth * 0.5f, height * 0.5f);
			kit.Gable(Art.Roof, roof, Art.Plaster, WHITE, new Vector3(0f, height + 0.2f, 0f), width, depth, depth * 0.42f, 0.4f);
			kit.Solid(Vector3.zero, new Vector3(width + 0.3f, height + 2f, depth + 0.3f));
			return kit;
		}

		private static void Door(VillageKit kit, float x, float z)
		{
			kit.Box(Art.Frame, WHITE, new Vector3(x, 0.2f, z), new Vector3(1.15f, 1.75f, 0.14f));
			kit.Box(Art.Wood, WHITE, new Vector3(x, 0.2f, z - 0.05f), new Vector3(0.9f, 1.6f, 0.12f));
			kit.Box(Art.Gold, WHITE, new Vector3(x + 0.3f, 0.95f, z - 0.12f), new Vector3(0.09f, 0.09f, 0.06f));
			kit.Box(Art.Stone, WHITE, new Vector3(x, 0f, z - 0.45f), new Vector3(1.3f, 0.14f, 0.7f));
		}

		private static void Window(VillageKit kit, float x, float z, float y)
		{
			kit.Box(Art.Frame, WHITE, new Vector3(x, y, z), new Vector3(0.82f, 0.82f, 0.12f));
			kit.Box(Art.Glow, WHITE, new Vector3(x, y + 0.1f, z - 0.04f), new Vector3(0.6f, 0.6f, 0.1f));
			kit.Box(Art.Frame, WHITE, new Vector3(x, y + 0.1f, z - 0.08f), new Vector3(0.06f, 0.6f, 0.08f));
			kit.Box(Art.Wood, WHITE, new Vector3(x, y - 0.1f, z - 0.14f), new Vector3(0.92f, 0.1f, 0.26f));
		}

		private static void Chimney(VillageKit kit, float x, float z, float top)
		{
			kit.Box(Art.Stone, WHITE, new Vector3(x, top - 1.4f, z), new Vector3(0.6f, 1.8f, 0.6f));
			kit.Box(Art.Stone, new Color(0.8f, 0.8f, 0.85f), new Vector3(x, top + 0.3f, z), new Vector3(0.76f, 0.16f, 0.76f));
		}

		private static void Fence(VillageKit kit, Vector3 from, Vector3 to, bool solid = true)
		{
			Vector3 delta = to - from;
			float length = delta.magnitude;
			int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.1f) + 1);
			float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
			for (int i = 0; i < posts; i++)
				kit.Box(Art.Wood, WHITE, Vector3.Lerp(from, to, i / (posts - 1f)), new Vector3(0.14f, 0.85f, 0.14f), new Vector3(0f, yaw, 0f));
			foreach (float height in new[] { 0.35f, 0.65f })
				kit.Box(Art.Wood, WHITE, (from + to) * 0.5f + Vector3.up * height, new Vector3(0.07f, 0.09f, length), new Vector3(0f, yaw, 0f));
			if (solid)
				kit.Solid((from + to) * 0.5f, new Vector3(0.2f, 1.2f, length), new Vector3(0f, yaw, 0f));
		}

		private static void Barrel(VillageKit kit, Vector3 foot)
		{
			kit.Prism(Art.Wood, WHITE, foot, new Vector3(0.6f, 0.8f, 0.6f), 8, 0.92f);
			kit.Prism(Art.Metal, WHITE, foot + Vector3.up * 0.2f, new Vector3(0.64f, 0.07f, 0.64f), 8);
			kit.Prism(Art.Metal, WHITE, foot + Vector3.up * 0.55f, new Vector3(0.62f, 0.07f, 0.62f), 8);
		}

		private static void Crate(VillageKit kit, Vector3 foot, float size, float yaw)
		{
			kit.Box(Art.Wood, WHITE, foot, Vector3.one * size, new Vector3(0f, yaw, 0f));
			kit.Box(Art.Wood, SOIL_DARK, foot + Vector3.up * size * 0.42f, new Vector3(size * 1.04f, size * 0.14f, size * 1.04f), new Vector3(0f, yaw, 0f));
		}

		// ---- 자리의 세 모습

		// 무너진 벽. 건물마다 같은 자리 크기, 씨앗으로 모양만 다르게
		public static VillageKit Ruin(Vector2 size, int seed)
		{
			System.Random random = new(seed);
			float Next(float from, float to) => from + (float)random.NextDouble() * (to - from);
			VillageKit kit = new();
			kit.Box(Art.Stone, WHITE, new Vector3(0f, -0.22f, 0f), new Vector3(size.x + 0.3f, 0.32f, size.y + 0.3f));
			int steps = Mathf.Max(3, Mathf.RoundToInt(size.x / 0.6f));
			for (int i = 0; i < steps; i++)
			{
				float x = Mathf.Lerp(-size.x * 0.5f, size.x * 0.5f, (i + 0.5f) / steps);
				float back = Mathf.Max(0.2f, 1.7f - Mathf.Abs(i - steps * 0.3f) * 0.35f + Next(-0.3f, 0.3f));
				kit.Box(Art.Stone, WHITE, new Vector3(x, 0.08f, size.y * 0.5f), new Vector3(size.x / steps, back, 0.35f));
				if (random.NextDouble() < 0.45)
					kit.Box(Art.Stone, WHITE, new Vector3(x, 0.08f, -size.y * 0.5f), new Vector3(size.x / steps, Next(0.15f, 0.5f), 0.35f));
			}
			int sideSteps = Mathf.Max(3, Mathf.RoundToInt(size.y / 0.6f));
			for (int i = 0; i < sideSteps; i++)
			{
				float z = Mathf.Lerp(size.y * 0.5f, -size.y * 0.5f, (i + 0.5f) / sideSteps);
				float height = Mathf.Max(0.12f, 1.4f - i * 0.28f + Next(-0.15f, 0.15f));
				kit.Box(Art.Stone, WHITE, new Vector3(-size.x * 0.5f, 0.08f, z), new Vector3(0.35f, height, size.y / sideSteps));
			}
			for (int i = 0; i < 7; i++)
			{
				float block = Next(0.25f, 0.5f);
				kit.Box(Art.Stone, WHITE, new Vector3(Next(-size.x * 0.4f, size.x * 0.6f), -0.05f, Next(-size.y * 0.5f, size.y * 0.3f)),
					new Vector3(block, block * 0.7f, block * 0.8f), new Vector3(Next(-12f, 12f), Next(0f, 180f), Next(-12f, 12f)));
			}
			kit.Box(Art.Wood, SOIL_DARK, new Vector3(size.x * 0.1f, 0.15f, 0.1f), new Vector3(size.x * 0.9f, 0.2f, 0.2f), new Vector3(0f, 24f, 14f));
			kit.Box(Art.Wood, SOIL_DARK, new Vector3(-size.x * 0.1f, 0.1f, -0.4f), new Vector3(size.x * 0.6f, 0.16f, 0.16f), new Vector3(0f, -38f, -6f));
			for (int i = 0; i < 5; i++)
				kit.Prism(Art.Leaf, WHITE, new Vector3(Next(-size.x * 0.5f, size.x * 0.5f), 0f, Next(-size.y * 0.5f, size.y * 0.5f)), new Vector3(0.5f, Next(0.25f, 0.5f), 0.5f), 6, 0.55f);
			kit.Solid(new Vector3(0f, 0f, size.y * 0.5f), new Vector3(size.x, 1.5f, 0.4f));
			kit.Solid(new Vector3(-size.x * 0.5f, 0f, size.y * 0.15f), new Vector3(0.4f, 1.2f, size.y * 0.7f));
			return kit;
		}

		// 공사 중. 다 되면 가림막이 앞을 덮음 (done)
		public static VillageKit Scaffold(Vector2 size, bool done)
		{
			VillageKit kit = new();
			float height = 2.6f;
			kit.Box(Art.Stone, WHITE, new Vector3(0f, -0.2f, 0f), new Vector3(size.x + 0.3f, 0.4f, size.y + 0.3f));
			for (int sideX = -1; sideX <= 1; sideX += 2)
			{
				for (int sideZ = -1; sideZ <= 1; sideZ += 2)
					kit.Box(Art.Wood, WHITE, new Vector3(sideX * size.x * 0.5f, 0f, sideZ * size.y * 0.5f), new Vector3(0.16f, height, 0.16f));
			}
			foreach (float level in new[] { 1.1f, 2.1f })
			{
				kit.Box(Art.Wood, WHITE, new Vector3(0f, level, size.y * 0.5f), new Vector3(size.x, 0.1f, 0.5f));
				kit.Box(Art.Wood, WHITE, new Vector3(-size.x * 0.5f, level, 0f), new Vector3(0.5f, 0.1f, size.y));
				kit.Box(Art.Wood, WHITE, new Vector3(size.x * 0.5f, level, 0f), new Vector3(0.5f, 0.1f, size.y));
			}
			kit.Box(Art.Plaster, WHITE, new Vector3(0f, 0.2f, size.y * 0.35f), new Vector3(size.x * 0.8f, done ? 2.2f : 1.1f, 0.2f));
			Crate(kit, new Vector3(size.x * 0.5f + 0.7f, 0f, -size.y * 0.3f), 0.6f, 18f);
			if (done)
			{
				kit.Box(Art.Cloth, CANVAS, new Vector3(0f, 0.1f, -size.y * 0.5f), new Vector3(size.x + 0.2f, height, 0.08f));
				kit.Box(Art.Cloth, CANVAS * 0.92f, new Vector3(-size.x * 0.5f, 0.1f, 0f), new Vector3(0.08f, height, size.y));
				kit.Box(Art.Cloth, CANVAS * 0.92f, new Vector3(size.x * 0.5f, 0.1f, 0f), new Vector3(0.08f, height, size.y));
				kit.Box(Art.Cloth, CANVAS * 1.02f, new Vector3(0f, height, 0f), new Vector3(size.x + 0.3f, 0.1f, size.y + 0.3f));
				kit.Box(Art.GlowPurple, WHITE, new Vector3(0f, height * 0.5f, -size.y * 0.5f - 0.06f), new Vector3(0.5f, 0.5f, 0.06f), new Vector3(0f, 0f, 45f));
			}
			kit.Solid(Vector3.zero, new Vector3(size.x, height, size.y));
			return kit;
		}

		public static VillageKit Built(string id, int level)
		{
			switch (id)
			{
				case "farm":
					return Shed(ROOF_HONEY);
				case "workshop":
					return Workshop();
				case "dock":
					return Dock();
				case "quarry":
					return Quarry();
				case "museum":
					return Museum();
				case "ranch":
					return Barn(level);
				case "tailor":
					return Tailor();
				case "training":
					return Training();
				case "shop":
					return Shop();
				case "smithy":
					return Smithy();
				case "atelier":
					return Atelier();
				case "trial":
					return Trial();
				default:
					throw new System.ArgumentOutOfRangeException(nameof(id), id, null);
			}
		}

		private static VillageKit Shed(Color roof)
		{
			VillageKit kit = House(2.6f, 2.2f, 1.9f, roof);
			Barrel(kit, new Vector3(1.8f, 0f, -0.6f));
			kit.Prism(Art.Soil, WHITE, new Vector3(-1.9f, 0f, -0.5f), new Vector3(1f, 0.4f, 1f), 7, 0.4f);
			return kit;
		}

		private static VillageKit Workshop()
		{
			VillageKit kit = House(4f, 3f, 2.3f, ROOF_TEAL);
			Chimney(kit, 1.2f, 0.5f, 4f);
			for (int i = 0; i < 5; i++)
				kit.Prism(Art.Wood, WHITE, new Vector3(2.8f + (i % 3) * 0.28f, 0.02f + (i / 3) * 0.26f, -1.2f), new Vector3(0.28f, 1.8f, 0.28f), 8, 1f, new Vector3(90f, 0f, 0f));
			kit.Box(Art.Wood, WHITE, new Vector3(-2.8f, 0f, -0.9f), new Vector3(1.1f, 0.75f, 0.7f));
			kit.Box(Art.Metal, WHITE, new Vector3(-2.8f, 0.75f, -0.9f), new Vector3(0.5f, 0.28f, 0.24f));
			return kit;
		}

		private static VillageKit Dock()
		{
			VillageKit kit = new();
			for (int i = 0; i < 8; i++)
				kit.Box(Art.Wood, i % 2 == 0 ? WHITE : SOIL_DARK * 1.15f, new Vector3(0f, 0.12f, -1.6f - i * 0.46f), new Vector3(1.9f, 0.1f, 0.42f));
			for (int i = 0; i < 3; i++)
			{
				for (int side = -1; side <= 1; side += 2)
					kit.Prism(Art.Wood, SOIL_DARK, new Vector3(side * 0.95f, -0.8f, -2f - i * 1.4f), new Vector3(0.2f, 1.4f, 0.2f), 6);
			}
			kit.Box(Art.Wood, WHITE, new Vector3(0.9f, 0.22f, -5f), new Vector3(0.1f, 1.5f, 0.1f));
			kit.Box(Art.Glow, WHITE, new Vector3(0.9f, 1.55f, -5f), new Vector3(0.24f, 0.3f, 0.24f));
			Barrel(kit, new Vector3(-0.6f, 0.2f, -1.9f));
			return kit;
		}

		private static VillageKit Quarry()
		{
			VillageKit kit = new();
			kit.Prism(Art.Stone, WHITE, new Vector3(0f, -0.3f, 1.6f), new Vector3(7f, 3.6f, 4.4f), 7, 0.55f);
			kit.Prism(Art.Stone, new Color(0.86f, 0.86f, 0.9f), new Vector3(-2.6f, -0.3f, 0.8f), new Vector3(2.8f, 2f, 2.4f), 6, 0.5f);
			kit.Prism(Art.Stone, new Color(0.9f, 0.88f, 0.86f), new Vector3(2.7f, -0.3f, 0.6f), new Vector3(2.4f, 1.5f, 2.2f), 6, 0.55f);
			kit.Box(Art.Dark, WHITE, new Vector3(0f, 0f, -0.62f), new Vector3(1.8f, 2.1f, 0.6f));
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Wood, WHITE, new Vector3(side * 1f, 0f, -0.85f), new Vector3(0.3f, 2.2f, 0.3f));
			kit.Box(Art.Wood, WHITE, new Vector3(0f, 2.1f, -0.85f), new Vector3(2.7f, 0.32f, 0.36f));
			kit.Box(Art.Glow, WHITE, new Vector3(-1.2f, 1.5f, -1.05f), new Vector3(0.22f, 0.3f, 0.22f));
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Metal, WHITE, new Vector3(side * 0.4f, 0.02f, -1.9f), new Vector3(0.08f, 0.08f, 2.4f));
			for (int i = 0; i < 5; i++)
				kit.Box(Art.Wood, SOIL_DARK, new Vector3(0f, -0.02f, -0.9f - i * 0.5f), new Vector3(1.2f, 0.08f, 0.2f));
			kit.Crystal(Art.GlowPurple, WHITE, new Vector3(2f, 0.4f, -0.9f), new Vector3(0.5f, 1f, 0.5f), 6, new Vector3(0f, 0f, -18f));
			kit.Solid(new Vector3(0f, 0f, 1.5f), new Vector3(7f, 3f, 4f));
			return kit;
		}

		private static VillageKit Museum()
		{
			VillageKit kit = House(6f, 4f, 3f, ROOF_PURPLE);
			for (int i = 0; i < 4; i++)
			{
				float x = -2.4f + i * 1.6f;
				kit.Prism(Art.Plaster, WHITE, new Vector3(x, 0.1f, -2.9f), new Vector3(0.4f, 2.9f, 0.4f), 8, 0.88f);
				kit.Box(Art.Gold, WHITE, new Vector3(x, 2.9f, -2.9f), new Vector3(0.56f, 0.14f, 0.56f));
				kit.Box(Art.Stone, WHITE, new Vector3(x, 0f, -2.9f), new Vector3(0.6f, 0.18f, 0.6f));
			}
			kit.Box(Art.Frame, WHITE, new Vector3(0f, 3.05f, -2.5f), new Vector3(6.2f, 0.3f, 1.5f));
			kit.Box(Art.Stone, WHITE, new Vector3(0f, -0.12f, -2.8f), new Vector3(6.4f, 0.24f, 1.6f));
			kit.Crystal(Art.GlowPurple, WHITE, new Vector3(0f, 3.4f, -2.9f), new Vector3(0.6f, 1f, 0.6f), 6);
			return kit;
		}

		private static VillageKit Barn(int level)
		{
			VillageKit kit = House(3.4f, 3f, 2.4f, ROOF_ROSE);
			kit.Box(Art.Wood, WHITE, new Vector3(0f, 2.2f, -1.56f), new Vector3(1f, 0.8f, 0.1f));
			float yard = 3f + level * 0.8f;
			Vector3 a = new(-2.2f, 0f, -2f);
			Vector3 b = new(-2.2f, 0f, -2f - yard);
			Vector3 c = new(2.8f + yard * 0.4f, 0f, -2f - yard);
			Vector3 d = new(2.8f + yard * 0.4f, 0f, -2f);
			Fence(kit, a, b);
			Fence(kit, b, b + (c - b) * 0.36f);
			Fence(kit, b + (c - b) * 0.64f, c);
			Fence(kit, c, d);
			kit.Box(Art.Wood, WHITE, new Vector3(1.8f, 0f, -2.6f), new Vector3(1.4f, 0.4f, 0.5f));
			kit.Box(Art.Cloth, new Color(0.95f, 0.84f, 0.5f), new Vector3(-1.2f, 0f, -2.6f), new Vector3(0.9f, 0.6f, 0.7f), new Vector3(0f, 12f, 0f));
			return kit;
		}

		private static VillageKit Tailor()
		{
			VillageKit kit = House(3.6f, 2.8f, 2.3f, ROOF_ROSE);
			for (int i = 0; i < 6; i++)
				kit.Box(Art.Cloth, i % 2 == 0 ? ROOF_ROSE : WHITE, new Vector3(-1.5f + i * 0.6f, 2f, -1.85f), new Vector3(0.6f, 0.07f, 1f), new Vector3(-18f, 0f, 0f));
			kit.Box(Art.Wood, WHITE, new Vector3(2.4f, 0f, -0.8f), new Vector3(0.12f, 1.6f, 0.12f));
			kit.Prism(Art.Cloth, new Color(0.75f, 0.62f, 0.86f), new Vector3(2.4f, 0.6f, -0.8f), new Vector3(0.7f, 0.9f, 0.5f), 8, 0.6f);
			return kit;
		}

		private static VillageKit Training()
		{
			VillageKit kit = new();
			kit.Box(Art.Soil, SOIL_DARK * 1.3f, new Vector3(0f, -0.22f, 0f), new Vector3(5f, 0.26f, 4f));
			Fence(kit, new Vector3(-2.5f, 0f, 2f), new Vector3(2.5f, 0f, 2f));
			Fence(kit, new Vector3(-2.5f, 0f, -2f), new Vector3(-2.5f, 0f, 2f));
			for (int i = -1; i <= 1; i++)
			{
				kit.Box(Art.Wood, WHITE, new Vector3(i * 1.5f, 0f, 0.9f), new Vector3(0.16f, 1.5f, 0.16f));
				kit.Prism(Art.Cloth, CANVAS, new Vector3(i * 1.5f, 0.5f, 0.9f), new Vector3(0.6f, 0.8f, 0.5f), 8, 0.85f);
				kit.Prism(Art.Cloth, CANVAS * 0.95f, new Vector3(i * 1.5f, 1.35f, 0.9f), new Vector3(0.4f, 0.36f, 0.4f), 8, 0.8f);
				kit.Box(Art.Wood, WHITE, new Vector3(i * 1.5f, 1f, 0.9f), new Vector3(1.1f, 0.1f, 0.1f));
				kit.Solid(new Vector3(i * 1.5f, 0f, 0.9f), new Vector3(0.6f, 1.6f, 0.5f));
			}
			kit.Box(Art.Frame, WHITE, new Vector3(2f, 0f, -1.4f), new Vector3(0.9f, 1.1f, 0.4f));
			return kit;
		}

		private static VillageKit Shop()
		{
			VillageKit kit = House(3.2f, 2.6f, 2.2f, ROOF_HONEY);
			kit.Box(Art.Wood, WHITE, new Vector3(-2.6f, 0f, -0.6f), new Vector3(1.6f, 0.8f, 0.9f), default, true);
			for (int i = 0; i < 4; i++)
				kit.Box(Art.Cloth, i % 2 == 0 ? ROOF_TEAL : WHITE, new Vector3(-3.2f + i * 0.42f, 1.8f, -0.6f), new Vector3(0.42f, 0.07f, 1.4f), new Vector3(-12f, 0f, 0f));
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Wood, WHITE, new Vector3(-2.6f + side * 0.8f, 0f, -1.1f), new Vector3(0.1f, 1.8f, 0.1f));
			Crate(kit, new Vector3(-2.9f, 0.8f, -0.6f), 0.3f, 10f);
			Crate(kit, new Vector3(2.3f, 0f, -0.7f), 0.6f, -14f);
			return kit;
		}

		private static VillageKit Smithy()
		{
			VillageKit kit = House(3.6f, 3f, 2.2f, ROOF_SLATE);
			Chimney(kit, -1.1f, 0.4f, 4.3f);
			kit.Box(Art.Stone, WHITE, new Vector3(2.6f, 0f, -0.4f), new Vector3(1.2f, 1.1f, 1.2f), default, true);
			kit.Box(Art.Glow, new Color(1f, 0.6f, 0.3f), new Vector3(2.6f, 0.35f, -1.02f), new Vector3(0.6f, 0.4f, 0.06f));
			kit.Prism(Art.Wood, WHITE, new Vector3(2.4f, 0f, -2f), new Vector3(0.6f, 0.5f, 0.6f), 8);
			kit.Box(Art.Metal, WHITE, new Vector3(2.4f, 0.5f, -2f), new Vector3(0.7f, 0.28f, 0.32f));
			Barrel(kit, new Vector3(-2.3f, 0f, -0.8f));
			return kit;
		}

		private static VillageKit Atelier()
		{
			VillageKit kit = House(3.8f, 3f, 2.5f, ROOF_PURPLE);
			kit.Box(Art.Frame, WHITE, new Vector3(0f, 2.1f, -1.58f), new Vector3(1.7f, 0.9f, 0.12f));
			kit.Box(Art.Glow, WHITE, new Vector3(0f, 2.2f, -1.64f), new Vector3(1.5f, 0.7f, 0.08f));
			for (int i = 0; i < 3; i++)
			{
				kit.Prism(Art.Wood, WHITE, new Vector3(2.4f, 0.1f + i * 0.42f, -0.8f + i * 0.1f), new Vector3(0.5f, 0.36f, 0.5f), 8);
				kit.Prism(Art.Cloth, i == 0 ? ROOF_ROSE : i == 1 ? ROOF_TEAL : ROOF_HONEY, new Vector3(2.4f, 0.14f + i * 0.42f, -0.8f + i * 0.1f), new Vector3(0.54f, 0.26f, 0.54f), 10);
			}
			return kit;
		}

		private static VillageKit Trial()
		{
			VillageKit kit = new();
			kit.Prism(Art.Stone, WHITE, new Vector3(0f, -0.2f, 0f), new Vector3(5f, 0.36f, 5f), 12);
			for (int i = 0; i < 6; i++)
			{
				float angle = i * Mathf.PI / 3f + Mathf.PI / 6f;
				Vector3 foot = new(Mathf.Cos(angle) * 2.1f, 0.1f, Mathf.Sin(angle) * 2.1f);
				kit.Prism(Art.Stone, WHITE, foot, new Vector3(0.6f, 2.2f + (i % 2) * 0.5f, 0.6f), 5, 0.6f);
				kit.Box(Art.GlowPurple, WHITE, foot + Vector3.up * 1.1f - new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.26f, new Vector3(0.12f, 0.5f, 0.12f));
				kit.Solid(foot, new Vector3(0.6f, 2f, 0.6f));
			}
			kit.Crystal(Art.GlowPurple, WHITE, new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 1.4f, 0.7f), 6);
			return kit;
		}

		// 회관. 단이 오를수록 커짐
		public static VillageKit TownHall(int tier)
		{
			if (tier == 1)
			{
				VillageKit tent = new();
				tent.Gable(Art.Cloth, CANVAS, Art.Cloth, CANVAS * 0.9f, Vector3.zero, 4f, 3.4f, 2.4f, 0.2f);
				for (int side = -1; side <= 1; side += 2)
					tent.Box(Art.Wood, WHITE, new Vector3(side * 2.1f, 0f, 0f), new Vector3(0.16f, 2.6f, 0.16f));
				tent.Box(Art.Wood, WHITE, new Vector3(2.9f, 0f, -0.8f), new Vector3(1f, 0.7f, 0.7f));
				Crate(tent, new Vector3(-2.9f, 0f, -0.6f), 0.7f, 20f);
				tent.Solid(Vector3.zero, new Vector3(3.6f, 2f, 2.2f));
				return tent;
			}
			float width = 4.4f + tier * 0.7f;
			float depth = 3.4f + tier * 0.4f;
			float height = 2.2f + tier * 0.3f;
			VillageKit kit = House(width, depth, height, tier >= 4 ? ROOF_PURPLE : ROOF_SLATE);
			kit.Box(Art.Wood, WHITE, new Vector3(0f, height + 0.1f, -depth * 0.5f - 0.5f), new Vector3(2.2f, 0.12f, 1.2f), new Vector3(-10f, 0f, 0f));
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Frame, WHITE, new Vector3(side * 1f, 0f, -depth * 0.5f - 0.9f), new Vector3(0.18f, height, 0.18f));
			Chimney(kit, width * 0.3f, depth * 0.1f, height + depth * 0.4f);
			if (tier >= 3)
			{
				kit.Box(Art.Plaster, WHITE, new Vector3(-width * 0.5f - 0.6f, 0f, 0.2f), new Vector3(1.6f, height + 1.8f, 1.6f));
				kit.Prism(Art.Roof, ROOF_PURPLE, new Vector3(-width * 0.5f - 0.6f, height + 1.8f, 0.2f), new Vector3(2.2f, 1.6f, 2.2f), 4, 0.04f, new Vector3(0f, 45f, 0f));
			}
			if (tier >= 5)
				kit.Crystal(Art.GlowPurple, WHITE, new Vector3(0f, height + depth * 0.42f + 0.6f, 0f), new Vector3(0.9f, 1.6f, 0.9f), 6);
			return kit;
		}

		// 집. 단이 오를수록 넓어짐. 겉모습만, 안은 따로 있는 방
		public static VillageKit Home(int tier)
		{
			float width = 3.4f + tier * 0.5f;
			VillageKit kit = House(width, 3f + tier * 0.25f, 2.2f + tier * 0.12f, ROOF_PURPLE);
			Chimney(kit, width * 0.28f, 0.4f, 3.8f + tier * 0.2f);
			for (int i = 0; i < 3; i++)
			{
				kit.Prism(Art.Stone, WHITE, new Vector3(-width * 0.5f - 0.5f, 0f, -0.9f + i * 0.8f), new Vector3(0.4f, 0.35f, 0.4f), 6, 1.15f);
				kit.Prism(Art.Leaf, WHITE, new Vector3(-width * 0.5f - 0.5f, 0.3f, -0.9f + i * 0.8f), new Vector3(0.45f, 0.45f, 0.45f), 6, 0.5f);
			}
			kit.Prism(Art.Dark, WHITE, new Vector3(width * 0.5f + 0.9f, 0f, -0.6f), new Vector3(0.9f, 0.7f, 0.9f), 9, 1.12f);
			kit.Prism(Art.GlowPurple, WHITE, new Vector3(width * 0.5f + 0.9f, 0.62f, -0.6f), new Vector3(0.8f, 0.06f, 0.8f), 9);
			return kit;
		}

		public static VillageKit Board()
		{
			VillageKit kit = new();
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Frame, WHITE, new Vector3(side * 0.95f, 0f, 0f), new Vector3(0.16f, 2f, 0.16f));
			kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.8f, 0f), new Vector3(1.9f, 1.1f, 0.1f));
			kit.Box(Art.Roof, ROOF_PURPLE, new Vector3(0f, 1.95f, -0.1f), new Vector3(2.4f, 0.1f, 0.7f), new Vector3(-12f, 0f, 0f));
			for (int i = 0; i < 4; i++)
				kit.Box(Art.Cloth, CANVAS * 1.04f, new Vector3(-0.6f + i * 0.4f, 1.05f + (i % 2) * 0.2f, -0.07f), new Vector3(0.3f, 0.4f, 0.02f), new Vector3(0f, 0f, i % 2 == 0 ? 4f : -5f));
			kit.Solid(Vector3.zero, new Vector3(2f, 2f, 0.3f));
			return kit;
		}

		public static VillageKit Well()
		{
			VillageKit kit = new();
			kit.Prism(Art.Stone, WHITE, Vector3.zero, new Vector3(1.9f, 0.8f, 1.9f), 10);
			kit.Prism(Art.Glass, new Color(0.5f, 0.8f, 1f), new Vector3(0f, 0.74f, 0f), new Vector3(1.5f, 0.08f, 1.5f), 10);
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Wood, WHITE, new Vector3(side * 0.8f, 0.6f, 0f), new Vector3(0.14f, 1.5f, 0.14f));
			kit.Gable(Art.Roof, ROOF_ROSE, Art.Wood, WHITE, new Vector3(0f, 2f, 0f), 1.9f, 1.5f, 0.6f, 0.15f);
			kit.Solid(Vector3.zero, new Vector3(1.7f, 1.4f, 1.7f));
			return kit;
		}

		public static VillageKit Pedestal()
		{
			VillageKit kit = new();
			kit.Prism(Art.Stone, WHITE, Vector3.zero, new Vector3(0.7f, 0.14f, 0.7f), 8);
			kit.Prism(Art.Plaster, WHITE, new Vector3(0f, 0.12f, 0f), new Vector3(0.44f, 0.46f, 0.44f), 8, 0.9f);
			kit.Prism(Art.Gold, WHITE, new Vector3(0f, 0.56f, 0f), new Vector3(0.56f, 0.07f, 0.56f), 8);
			return kit;
		}

		public static VillageKit Lamp()
		{
			VillageKit kit = new();
			kit.Prism(Art.Stone, WHITE, Vector3.zero, new Vector3(0.36f, 0.16f, 0.36f), 6);
			kit.Box(Art.Frame, WHITE, Vector3.zero, new Vector3(0.1f, 2f, 0.1f));
			kit.Box(Art.Frame, WHITE, new Vector3(0.2f, 1.9f, 0f), new Vector3(0.5f, 0.08f, 0.08f));
			kit.Box(Art.Glow, WHITE, new Vector3(0.4f, 1.5f, 0f), new Vector3(0.22f, 0.32f, 0.22f));
			kit.Prism(Art.Frame, WHITE, new Vector3(0.4f, 1.82f, 0f), new Vector3(0.36f, 0.14f, 0.36f), 4, 0.2f);
			return kit;
		}
	}
}
