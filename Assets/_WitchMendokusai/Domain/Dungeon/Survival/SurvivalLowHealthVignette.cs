using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 생존 판에서 체력이 낮으면 화면 가장자리를 붉게 맥동. 화면 위 UI 이미지 하나 (가운데 투명 방사형 텍스처)
	// 후처리 Volume 은 이 카메라 설정에서 반영되지 않아 UI 로 (2026-11-12 MOIS2 실측)
	public class SurvivalLowHealthVignette : MonoBehaviour
	{
		private const int TEXTURE_SIZE = 256;

		private ResourceManager resourceManager;
		private PlayerProvider playerProvider;
		private Image image;
		private float weight;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;

		public void Init(ResourceManager resourceManager, PlayerProvider playerProvider)
		{
			this.resourceManager = resourceManager;
			this.playerProvider = playerProvider;
			SurvivalFeelConfig config = Config;

			GameObject canvasObject = new("Low health vignette", typeof(Canvas));
			canvasObject.transform.SetParent(transform, false);
			Canvas canvas = canvasObject.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = config.LowHealthSortingOrder;

			GameObject imageObject = new("Edge", typeof(RectTransform), typeof(Image));
			RectTransform rect = (RectTransform)imageObject.transform;
			rect.SetParent(canvasObject.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			image = imageObject.GetComponent<Image>();
			image.raycastTarget = false;
			image.sprite = BuildEdgeSprite(config.LowHealthInnerRadius, config.LowHealthEdgePower);
			image.color = Color.clear;
		}

		private static Sprite BuildEdgeSprite(float innerRadius, float power)
		{
			Texture2D texture = new(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LowHealthEdge" };
			Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];
			float half = (TEXTURE_SIZE - 1) * 0.5f;
			for (int y = 0; y < TEXTURE_SIZE; y++)
			{
				for (int x = 0; x < TEXTURE_SIZE; x++)
				{
					float dx = (x - half) / half;
					float dy = (y - half) / half;
					float distance = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
					float alpha = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(innerRadius, 1f, distance)), power);
					pixels[y * TEXTURE_SIZE + x] = new Color(1f, 1f, 1f, alpha);
				}
			}
			texture.SetPixels(pixels);
			texture.Apply();
			return Sprite.Create(texture, new Rect(0, 0, TEXTURE_SIZE, TEXTURE_SIZE), new Vector2(0.5f, 0.5f));
		}

		private void Update()
		{
			if (image == null)
				return;
			SurvivalFeelConfig config = Config;
			float target = 0f;
			PlayerObject player = playerProvider.CurrentObject;
			bool inRun = DungeonManager.Instance != null && DungeonManager.Instance.IsDungeon;
			if (inRun && player != null && player.UnitStat[UnitStatType.HP_MAX] > 0)
			{
				float ratio = (float)player.UnitStat[UnitStatType.HP_CUR] / player.UnitStat[UnitStatType.HP_MAX];
				if (ratio < config.LowHealthThreshold)
				{
					float danger = 1f - ratio / config.LowHealthThreshold;
					float pulse = 1f - config.LowHealthPulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * config.LowHealthPulseSpeed * Mathf.PI * 2f));
					target = Mathf.Lerp(config.LowHealthMinWeight, 1f, danger) * pulse;
				}
			}
			weight = Mathf.MoveTowards(weight, target, config.LowHealthFadeSpeed * Time.unscaledDeltaTime);
			Color color = config.LowHealthColor;
			color.a *= weight * config.LowHealthMaxAlpha;
			image.color = color;
		}
	}
}
