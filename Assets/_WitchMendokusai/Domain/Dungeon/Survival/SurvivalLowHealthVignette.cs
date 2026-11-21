using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 생존 판 화면 가장자리 빨강. 캔버스 하나에 이미지 둘 (가운데 투명 방사형 텍스처)
	// - 저체력 경고: 체력이 낮으면 맥동 (설정에서 끌 수 있음)
	// - 피격 번쩍임: 피격 직후 짙게, 잠깐 머문 뒤 사라짐 (PlayerHitFeel 이 Hit 호출). 저체력보다 안쪽까지 물듦
	// 후처리 Volume 은 이 카메라 설정에서 반영되지 않아 UI 로 (2026-11-12 MOIS2 실측)
	public class SurvivalLowHealthVignette : MonoBehaviour
	{
		private const int TEXTURE_SIZE = 256;

		private ResourceManager resourceManager;
		private PlayerProvider playerProvider;
		private Image image;
		private Image hitImage;
		private float weight;
		private float hitPeak;
		private float hitStartTime = float.NegativeInfinity;

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

			image = BuildEdgeImage("Edge", canvasObject.transform, config.LowHealthInnerRadius, config.LowHealthEdgePower);
			hitImage = BuildEdgeImage("Hit edge", canvasObject.transform, config.PlayerHit.EdgeInnerRadius, config.LowHealthEdgePower);
		}

		/// <summary> 피격 직후 가장자리 번쩍임. 사라지는 중에 오면 더 짙은 쪽으로 다시 시작 </summary>
		public void Hit(float alpha)
		{
			float current = HitAlpha(Time.unscaledTime);
			hitPeak = Mathf.Clamp01(Mathf.Max(alpha, current));
			hitStartTime = Time.unscaledTime;
		}

		public float HitAlpha(float now)
		{
			PlayerHitFeelSettings settings = Config.PlayerHit;
			float age = now - hitStartTime;
			if (age < 0f || hitPeak <= 0f)
				return 0f;
			if (age <= settings.EdgeHoldSeconds)
				return hitPeak;
			float fade = Mathf.Clamp01((age - settings.EdgeHoldSeconds) / settings.EdgeFadeSeconds);
			return hitPeak * (1f - fade) * (1f - fade);
		}

		private static Image BuildEdgeImage(string imageName, Transform parent, float innerRadius, float power)
		{
			GameObject imageObject = new(imageName, typeof(RectTransform), typeof(Image));
			RectTransform rect = (RectTransform)imageObject.transform;
			rect.SetParent(parent, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			Image edgeImage = imageObject.GetComponent<Image>();
			edgeImage.raycastTarget = false;
			edgeImage.sprite = BuildEdgeSprite(innerRadius, power);
			edgeImage.color = Color.clear;
			return edgeImage;
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
			if (config.LowHealthWarning && inRun && player != null && player.UnitStat[UnitStatType.HP_MAX] > 0)
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

			Color hitColor = config.LowHealthColor;
			hitColor.a *= inRun ? HitAlpha(Time.unscaledTime) : 0f;
			hitImage.color = hitColor;
		}
	}
}
