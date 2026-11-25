using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 가구의 생김새. 자료의 Shape 이름과 크기, 결 (Theme) 색으로 지음. 앞은 -z
	public static class FurnitureBodies
	{
		private static readonly Color WHITE = Color.white;
		private static readonly Color INK = new(0.42f, 0.36f, 0.5f);
		private static readonly Color LEAF_LIGHT = new(0.85f, 1f, 0.75f);

		private static HomesteadArt Art => HomesteadArt.Instance;

		private static Color ThemeColor(string theme)
		{
			switch (theme)
			{
				case "바느질":
					return new Color(0.90f, 0.62f, 0.66f);
				case "풀꽃":
					return new Color(0.66f, 0.82f, 0.56f);
				case "별빛":
					return new Color(0.56f, 0.58f, 0.86f);
				case "마녀":
					return new Color(0.62f, 0.50f, 0.74f);
				case "물가":
					return new Color(0.54f, 0.76f, 0.84f);
				case "길드":
					return new Color(0.86f, 0.70f, 0.42f);
				default:
					return new Color(0.92f, 0.80f, 0.64f);
			}
		}

		public static VillageKit Make(FurnitureDef furniture)
		{
			VillageKit kit = new();
			float w = furniture.Width;
			float d = furniture.Depth;
			Color theme = ThemeColor(furniture.Theme);
			switch (furniture.Shape)
			{
				case "bed":
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.1f, 0f), new Vector3(w, 0.22f, d));
					kit.Box(Art.Cloth, WHITE, new Vector3(0f, 0.3f, 0f), new Vector3(w * 0.94f, 0.18f, d * 0.96f));
					kit.Box(Art.Cloth, theme, new Vector3(0f, 0.36f, -d * 0.14f), new Vector3(w * 0.98f, 0.18f, d * 0.68f));
					kit.Box(Art.Cloth, WHITE, new Vector3(0f, 0.46f, d * 0.36f), new Vector3(w * 0.6f, 0.14f, d * 0.16f));
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0f, d * 0.48f), new Vector3(w, 0.9f, 0.1f));
					break;
				case "table":
				case "desk":
					Legs(kit, w, d, 0.66f);
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.64f, 0f), new Vector3(w, 0.1f, d));
					if (furniture.Shape == "desk")
					{
						kit.Box(Art.Wood, theme, new Vector3(w * 0.3f, 0.2f, 0f), new Vector3(w * 0.34f, 0.44f, d * 0.9f));
						kit.Box(Art.Cloth, theme, new Vector3(-w * 0.15f, 0.74f, 0f), new Vector3(w * 0.4f, 0.02f, d * 0.6f));
						kit.Prism(Art.Glow, WHITE, new Vector3(w * 0.36f, 0.74f, d * 0.2f), new Vector3(0.14f, 0.24f, 0.14f), 6);
					}
					break;
				case "chair":
					Legs(kit, w, d, 0.36f);
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.34f, 0f), new Vector3(w, 0.08f, d));
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.4f, d * 0.44f), new Vector3(w, 0.5f, 0.07f));
					kit.Box(Art.Cloth, theme, new Vector3(0f, 0.42f, -0.02f), new Vector3(w * 0.8f, 0.06f, d * 0.75f));
					break;
				case "bench":
					Legs(kit, w, d, 0.36f);
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.34f, 0f), new Vector3(w, 0.1f, d));
					break;
				case "shelf":
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0f, d * 0.42f), new Vector3(w, 1.5f, 0.06f));
					for (int side = -1; side <= 1; side += 2)
						kit.Box(Art.Wood, WHITE, new Vector3(side * w * 0.48f, 0f, 0f), new Vector3(0.06f, 1.5f, d));
					for (int i = 0; i < 4; i++)
						kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.04f + i * 0.46f, 0f), new Vector3(w, 0.06f, d));
					Books(kit, w, 0.1f, theme, furniture.Id.Length);
					Books(kit, w, 0.56f, INK, furniture.Id.Length + 3);
					Books(kit, w, 1.02f, theme, furniture.Id.Length + 5);
					break;
				case "dresser":
					kit.Box(Art.Wood, WHITE, Vector3.zero, new Vector3(w, 0.8f, d));
					for (int i = 0; i < 3; i++)
					{
						kit.Box(Art.Wood, theme, new Vector3(0f, 0.08f + i * 0.24f, -d * 0.5f), new Vector3(w * 0.88f, 0.2f, 0.04f));
						kit.Box(Art.Gold, WHITE, new Vector3(0f, 0.15f + i * 0.24f, -d * 0.53f), new Vector3(0.1f, 0.05f, 0.04f));
					}
					break;
				case "rug":
					kit.Box(Art.Cloth, theme, Vector3.zero, new Vector3(w, 0.03f, d));
					kit.Box(Art.Cloth, WHITE, new Vector3(0f, 0.012f, 0f), new Vector3(w * 0.86f, 0.03f, d * 0.76f));
					kit.Box(Art.Cloth, theme * 0.9f, new Vector3(0f, 0.024f, 0f), new Vector3(w * 0.7f, 0.03f, d * 0.56f));
					break;
				case "rug_round":
					kit.Prism(Art.Cloth, theme, Vector3.zero, new Vector3(w, 0.03f, d), 20);
					kit.Prism(Art.Cloth, WHITE, new Vector3(0f, 0.012f, 0f), new Vector3(w * 0.8f, 0.03f, d * 0.8f), 20);
					kit.Prism(Art.Cloth, theme * 0.92f, new Vector3(0f, 0.024f, 0f), new Vector3(w * 0.56f, 0.03f, d * 0.56f), 20);
					break;
				case "cushion":
					kit.Prism(Art.Cloth, theme, Vector3.zero, new Vector3(w, 0.2f, d), 8, 0.82f);
					break;
				case "pot":
				case "vase":
				case "stand":
					kit.Prism(furniture.Shape == "vase" ? Art.Glass : Art.Stone, furniture.Shape == "vase" ? new Color(0.8f, 0.9f, 1f) : WHITE, Vector3.zero, new Vector3(w * 0.7f, 0.36f, d * 0.7f), 8, 1.2f);
					kit.Prism(Art.Leaf, LEAF_LIGHT, new Vector3(0f, 0.34f, 0f), new Vector3(w, 0.5f, d), 7, 0.4f);
					for (int i = 0; i < 4; i++)
						kit.Prism(Art.Cloth, i % 2 == 0 ? new Color(1f, 0.72f, 0.8f) : new Color(0.78f, 0.68f, 1f), new Vector3(Mathf.Cos(i * 1.6f) * w * 0.26f, 0.72f, Mathf.Sin(i * 1.6f) * d * 0.26f), new Vector3(0.14f, 0.12f, 0.14f), 6, 0.5f);
					break;
				case "planter":
					kit.Box(Art.Wood, WHITE, Vector3.zero, new Vector3(w, 0.34f, d));
					kit.Box(Art.Soil, WHITE, new Vector3(0f, 0.3f, 0f), new Vector3(w * 0.9f, 0.08f, d * 0.8f));
					for (int i = 0; i < 5; i++)
						kit.Prism(Art.Leaf, LEAF_LIGHT, new Vector3((i - 2) * w * 0.18f, 0.34f, 0f), new Vector3(0.28f, 0.34f + (i % 2) * 0.1f, 0.28f), 6, 0.4f);
					break;
				case "lamp":
					kit.Prism(Art.Frame, WHITE, Vector3.zero, new Vector3(w * 0.7f, 0.1f, d * 0.7f), 8);
					kit.Box(Art.Frame, WHITE, Vector3.zero, new Vector3(0.08f, 1f, 0.08f));
					kit.Crystal(Art.GlowPurple, WHITE, new Vector3(0f, 0.9f, 0f), new Vector3(0.36f, 0.6f, 0.36f), 6);
					break;
				case "crystal":
					kit.Crystal(Art.GlowPurple, WHITE, new Vector3(0f, -0.2f, 0f), new Vector3(0.4f, 1.4f, 0.4f), 6);
					kit.Crystal(Art.GlowPurple, new Color(0.8f, 0.9f, 1f), new Vector3(0.22f, -0.1f, 0.06f), new Vector3(0.26f, 0.9f, 0.26f), 6, new Vector3(0f, 0f, -22f));
					kit.Crystal(Art.GlowPurple, new Color(1f, 0.85f, 1f), new Vector3(-0.2f, -0.1f, -0.08f), new Vector3(0.24f, 0.8f, 0.24f), 6, new Vector3(0f, 0f, 20f));
					break;
				case "globe":
					kit.Prism(Art.Gold, WHITE, Vector3.zero, new Vector3(w * 0.6f, 0.1f, d * 0.6f), 8);
					kit.Box(Art.Gold, WHITE, Vector3.zero, new Vector3(0.08f, 0.5f, 0.08f));
					kit.Prism(Art.Cloth, theme, new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.17f, 0.34f), 10, 1.5f);
					kit.Prism(Art.Cloth, theme, new Vector3(0f, 0.59f, 0f), new Vector3(0.51f, 0.17f, 0.51f), 10, 0.66f);
					break;
				case "telescope":
					for (int i = 0; i < 3; i++)
						kit.Box(Art.Wood, WHITE, new Vector3(Mathf.Cos(i * 2.1f) * 0.14f, 0f, Mathf.Sin(i * 2.1f) * 0.14f), new Vector3(0.06f, 0.9f, 0.06f), new Vector3(Mathf.Sin(i * 2.1f) * -12f, 0f, Mathf.Cos(i * 2.1f) * 12f));
					kit.Prism(Art.Gold, WHITE, new Vector3(0f, 0.7f, 0.35f), new Vector3(0.2f, 0.9f, 0.2f), 8, 1.3f, new Vector3(-55f, 0f, 0f));
					break;
				case "cauldron":
					kit.Prism(Art.Dark, WHITE, Vector3.zero, new Vector3(w * 0.8f, 0.6f, d * 0.8f), 9, 1.15f);
					kit.Prism(Art.GlowPurple, WHITE, new Vector3(0f, 0.55f, 0f), new Vector3(w * 0.82f, 0.06f, d * 0.82f), 9);
					break;
				case "books":
					for (int i = 0; i < 4; i++)
						kit.Box(Art.Cloth, i % 2 == 0 ? theme : INK, new Vector3(0f, i * 0.1f, 0f), new Vector3(w * 0.7f, 0.09f, d * 0.5f), new Vector3(0f, i * 23f, 0f));
					break;
				case "candle":
					kit.Prism(Art.Gold, WHITE, Vector3.zero, new Vector3(w * 0.7f, 0.06f, d * 0.7f), 8);
					kit.Box(Art.Gold, WHITE, Vector3.zero, new Vector3(0.06f, 0.6f, 0.06f));
					kit.Prism(Art.Cloth, WHITE, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.24f, 0.12f), 6);
					kit.Crystal(Art.Glow, WHITE, new Vector3(0f, 0.84f, 0f), new Vector3(0.08f, 0.16f, 0.08f), 6);
					break;
				case "fireplace":
					kit.Box(Art.Stone, WHITE, Vector3.zero, new Vector3(w, 1.2f, d));
					kit.Box(Art.Dark, WHITE, new Vector3(0f, 0.08f, -d * 0.42f), new Vector3(w * 0.56f, 0.7f, d * 0.3f));
					kit.Crystal(Art.Glow, new Color(1f, 0.7f, 0.4f), new Vector3(0f, 0.1f, -d * 0.48f), new Vector3(0.4f, 0.5f, 0.2f), 6);
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 1.2f, 0f), new Vector3(w * 1.06f, 0.1f, d * 1.1f));
					break;
				case "bowl":
					kit.Box(Art.Wood, WHITE, Vector3.zero, new Vector3(w * 0.8f, 0.5f, d * 0.8f));
					kit.Prism(Art.Glass, new Color(0.7f, 0.9f, 1f), new Vector3(0f, 0.5f, 0f), new Vector3(w * 0.6f, 0.4f, d * 0.6f), 10, 0.8f);
					kit.Box(Art.Glow, new Color(1f, 0.6f, 0.4f), new Vector3(0f, 0.64f, 0f), new Vector3(0.12f, 0.08f, 0.06f));
					break;
				case "tank":
					kit.Box(Art.Wood, WHITE, Vector3.zero, new Vector3(w, 0.4f, d));
					kit.Box(Art.Glass, new Color(0.6f, 0.86f, 1f), new Vector3(0f, 0.4f, 0f), new Vector3(w * 0.96f, 0.7f, d * 0.92f));
					kit.Box(Art.Frame, WHITE, new Vector3(0f, 1.08f, 0f), new Vector3(w, 0.08f, d));
					for (int i = 0; i < 4; i++)
						kit.Box(Art.Glow, i % 2 == 0 ? new Color(1f, 0.6f, 0.4f) : new Color(0.6f, 0.8f, 1f), new Vector3(-w * 0.32f + i * w * 0.2f, 0.56f + (i % 3) * 0.14f, 0f), new Vector3(0.16f, 0.1f, 0.06f));
					break;
				case "box":
					kit.Box(Art.Wood, WHITE, Vector3.zero, new Vector3(w * 0.8f, 0.5f, d * 0.8f));
					kit.Box(Art.Wood, theme, new Vector3(0f, 0.5f, 0f), new Vector3(w * 0.84f, 0.26f, d * 0.84f));
					kit.Prism(Art.Gold, WHITE, new Vector3(0f, 0.76f, 0f), new Vector3(0.22f, 0.06f, 0.22f), 8);
					break;
				case "statue":
					kit.Prism(Art.Stone, WHITE, Vector3.zero, new Vector3(w, 0.3f, d), 8);
					kit.Prism(Art.Gold, WHITE, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.8f, 0.4f), 8, 0.6f);
					kit.Prism(Art.Gold, WHITE, new Vector3(0f, 1.08f, 0f), new Vector3(0.36f, 0.34f, 0.36f), 8, 0.8f);
					break;
				case "window":
					kit.Box(Art.Frame, WHITE, new Vector3(0f, 1f, 0f), new Vector3(w, 0.9f, d));
					kit.Box(Art.Glow, WHITE, new Vector3(0f, 1.1f, -d * 0.3f), new Vector3(w * 0.8f, 0.7f, d * 0.6f));
					kit.Box(Art.Frame, WHITE, new Vector3(0f, 1.1f, -d * 0.6f), new Vector3(0.06f, 0.7f, 0.04f));
					break;
				case "hanging":
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 1.75f, 0f), new Vector3(w, 0.06f, 0.06f));
					kit.Box(Art.Cloth, theme, new Vector3(0f, 0.9f, 0f), new Vector3(w * 0.9f, 0.86f, 0.04f));
					kit.Box(Art.Cloth, WHITE, new Vector3(0f, 1.1f, -0.03f), new Vector3(w * 0.36f, w * 0.36f, 0.03f), new Vector3(0f, 0f, 45f));
					break;
				case "frame":
					kit.Box(Art.Gold, WHITE, new Vector3(0f, 1f, 0f), new Vector3(w, 0.7f, d));
					kit.Box(Art.Cloth, theme, new Vector3(0f, 1.08f, -d * 0.4f), new Vector3(w * 0.82f, 0.54f, d * 0.4f));
					break;
				case "rack":
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 1.4f, 0f), new Vector3(w, 0.08f, d));
					for (int i = 0; i < 4; i++)
						kit.Prism(furniture.Theme == "풀꽃" ? Art.Leaf : Art.Cloth, furniture.Theme == "풀꽃" ? LEAF_LIGHT : Color.Lerp(theme, WHITE, i * 0.2f), new Vector3((i - 1.5f) * w * 0.24f, 1.04f, 0f), new Vector3(0.16f, 0.34f, 0.16f), 7, 1.1f);
					break;
				case "bottles":
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 1.1f, 0f), new Vector3(w, 0.07f, d));
					for (int i = 0; i < 4; i++)
						kit.Prism(Art.Glow, i % 2 == 0 ? new Color(0.8f, 0.6f, 1f) : new Color(0.6f, 1f, 0.8f), new Vector3((i - 1.5f) * w * 0.23f, 1.17f, 0f), new Vector3(0.16f, 0.26f, 0.16f), 7, 0.5f);
					break;
				case "broom":
					kit.Box(Art.Wood, WHITE, new Vector3(0f, 0.7f, 0f), new Vector3(0.06f, 1.1f, 0.06f));
					kit.Prism(Art.Cloth, new Color(0.92f, 0.76f, 0.44f), new Vector3(0f, 0.4f, 0f), new Vector3(0.32f, 0.4f, 0.2f), 7, 0.5f);
					break;
				case "sconce":
					kit.Box(Art.Frame, WHITE, new Vector3(0f, 1.3f, d * 0.3f), new Vector3(w * 0.5f, 0.3f, 0.08f));
					kit.Crystal(Art.Glow, WHITE, new Vector3(0f, 1.3f, -d * 0.1f), new Vector3(0.24f, 0.42f, 0.24f), 5);
					break;
				default:
					throw new System.ArgumentOutOfRangeException(nameof(furniture), furniture.Shape, null);
			}
			return kit;
		}

		private static void Legs(VillageKit kit, float width, float depth, float height)
		{
			for (int sideX = -1; sideX <= 1; sideX += 2)
			{
				for (int sideZ = -1; sideZ <= 1; sideZ += 2)
					kit.Box(Art.Wood, WHITE, new Vector3(sideX * (width * 0.5f - 0.06f), 0f, sideZ * (depth * 0.5f - 0.06f)), new Vector3(0.08f, height, 0.08f));
			}
		}

		private static void Books(VillageKit kit, float width, float height, Color color, int seed)
		{
			System.Random random = new(seed);
			float x = -width * 0.42f;
			while (x < width * 0.4f)
			{
				float thickness = 0.06f + (float)random.NextDouble() * 0.05f;
				if (random.NextDouble() < 0.82)
					kit.Box(Art.Cloth, Color.Lerp(color, WHITE, (float)random.NextDouble() * 0.5f), new Vector3(x, height, 0f), new Vector3(thickness, 0.24f + (float)random.NextDouble() * 0.1f, 0.24f));
				x += thickness + 0.015f;
			}
		}
	}
}
