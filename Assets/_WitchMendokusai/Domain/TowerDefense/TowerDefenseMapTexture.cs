using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 판의 지형을 한 장의 그림으로 굽는다 — 지도가 「점 몇 개」가 아니라 *땅*을 보여주게 (TASK-WM-194).
	///
	/// ★ 사용자 실증: "미니맵 좀 더 자세히 표시할 필요가 있음. 땅이라던지."
	///   점만 찍힌 지도는 「내 것이 어디 있나」는 알려주지만 「어디로 넓힐 수 있나」는 말해주지 않는다.
	///   암반이 어디를 막고 있는지, 금빛 자리가 어느 방향에 몰려 있는지가 개척의 판단 재료다.
	///
	/// ★ 왜 텍스처인가: 암반이 7천 칸을 넘는다 — 칸마다 UI 요소를 만들면 지도를 여는 순간 판이 멈춘다.
	///   땅은 *잘 안 변하므로* 한 번 굽고 재사용하고, 판이 자랄 때만 다시 굽는다.
	/// </summary>
	public static class TowerDefenseMapTexture
	{
		/// <summary>
		/// 지형 그림을 굽는다 — 칸 하나가 픽셀 하나다. 안 밝힌 자리도 그린다(땅 모양은 비밀이 아니다,
		/// 비밀은 *거기 무엇이 있나*이고 그건 점이 담당한다).
		/// </summary>
		public static Texture2D Bake(TowerDefenseMapLayout layout, Color ground, Color obstacle, Color node)
		{
			if (layout == null || layout.Width <= 0 || layout.Length <= 0)
				return null;

			Texture2D texture = new Texture2D(layout.Width, layout.Length, TextureFormat.RGBA32, mipChain: false)
			{
				// 칸이 픽셀이라 뭉개면 격자가 사라진다 — 개척은 칸 단위 판단이다.
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "TowerDefenseMap",
			};

			Color32[] pixels = new Color32[layout.Width * layout.Length];
			Color32 groundColor = ground;
			Color32 obstacleColor = obstacle;

			for (int z = 0; z < layout.Length; z++)
			{
				for (int x = 0; x < layout.Width; x++)
					pixels[z * layout.Width + x] = layout.IsBlocked(new Vector2Int(x, z)) ? obstacleColor : groundColor;
			}

			// 금빛 자리는 땅 위에 덧그린다 — 개척의 목적지라 지형보다 눈에 먼저 들어와야 한다.
			Color32 nodeColor = node;
			foreach (TowerDefenseResourceNodeSpot spot in layout.ResourceNodes)
			{
				if (spot.Cell.x < 0 || spot.Cell.x >= layout.Width || spot.Cell.y < 0 || spot.Cell.y >= layout.Length)
					continue;
				pixels[spot.Cell.y * layout.Width + spot.Cell.x] = nodeColor;
			}

			texture.SetPixels32(pixels);
			texture.Apply(updateMipmaps: false);
			return texture;
		}
	}
}
