using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 시야를 화면에 덮는 안개(TASK-WM-194).
	///
	/// ★ 칸마다 오브젝트를 만들지 않는다: 44×44 판이면 1900개가 넘고, 시야가 바뀔 때마다 그걸 전부 만지게 된다.
	///   판 전체를 덮는 판때기 1장 + 칸=픽셀 텍스처 1장이면, 시야 갱신은 픽셀 쓰기 한 번이다.
	///   점 필터(Point)라 픽셀 경계가 곧 칸 경계 — 「저 칸은 안 보인다」가 배치 격자와 정확히 맞는다.
	/// </summary>
	public sealed class TowerDefenseFogView : MonoBehaviour
	{
		private Texture2D texture;
		private Color32[] pixels;
		private int width;
		private int length;

		/// <summary> 판 크기에 맞춰 안개 판때기를 세운다. </summary>
		public static TowerDefenseFogView Create(Transform stageRoot, int width, int length, float groundWidth, float groundLength, float height)
		{
			GameObject fogObject = TowerDefenseVisuals.Primitive(PrimitiveType.Quad, unlit: true);
			fogObject.name = "Fog";
			Destroy(fogObject.GetComponent<Collider>()); // 표시용 — 배치 레이캐스트를 가로채면 안 된다.
			fogObject.transform.SetParent(stageRoot, false);
			fogObject.transform.localPosition = new Vector3(0f, height, 0f);
			fogObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			fogObject.transform.localScale = new Vector3(groundWidth, groundLength, 1f);

			TowerDefenseFogView fog = fogObject.AddComponent<TowerDefenseFogView>();
			fog.Initialize(width, length, fogObject.GetComponent<Renderer>());
			return fog;
		}

		private void Initialize(int cellWidth, int cellLength, Renderer fogRenderer)
		{
			width = Mathf.Max(1, cellWidth);
			length = Mathf.Max(1, cellLength);

			texture = new Texture2D(width, length, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};
			pixels = new Color32[width * length];
			for (int index = 0; index < pixels.Length; index++)
				pixels[index] = UnseenColor;
			texture.SetPixels32(pixels);
			texture.Apply();

			if (fogRenderer == null)
				return;

			Material material = TowerDefenseVisuals.CreateUnlit();
			material.mainTexture = texture;
			TowerDefenseVisuals.MakeTransparent(material);
			fogRenderer.sharedMaterial = material;
			fogRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			fogRenderer.receiveShadows = false;
		}

		/// <summary> 시야 상태를 그대로 그린다 — 안 가본 곳은 새까맣게, 기억하는 곳은 어둡게, 보이는 곳은 투명. </summary>
		public void Apply(TowerDefenseVision vision)
		{
			if (vision == null || texture == null)
				return;

			for (int y = 0; y < length; y++)
			{
				for (int x = 0; x < width; x++)
				{
					pixels[y * width + x] = vision.StateAt(new Vector2Int(x, y)) switch
					{
						TowerDefenseVisionState.Visible => ClearColor,
						TowerDefenseVisionState.Explored => ExploredColor,
						_ => UnseenColor,
					};
				}
			}

			texture.SetPixels32(pixels);
			texture.Apply();
		}

		// 안 가본 곳은 「지형조차 모른다」라 거의 불투명. 기억하는 곳은 지형이 비쳐야 하므로 반투명.
		// 안개는 확실히 어두워야 한다(사용자 지시) — 어스름하게만 덮으면 「안 보인다」가 아니라
		// 「좀 흐리다」로 읽혀, 시야를 넓히는 행위의 보상이 화면에서 사라진다.
		private static readonly Color32 UnseenColor = new Color32(2, 2, 4, 255);
		private static readonly Color32 ExploredColor = new Color32(3, 4, 8, 205);
		private static readonly Color32 ClearColor = new Color32(0, 0, 0, 0);

	}
}
