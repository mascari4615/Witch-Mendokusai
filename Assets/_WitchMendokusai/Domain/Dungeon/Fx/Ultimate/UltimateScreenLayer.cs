using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 동안 화면 위에 덮는 층. 전부 화면 위 이미지 (후처리 Volume 은 이 카메라에서 반영되지 않음)
	/// - 섬광 (더하기), 색 번짐 (더하기), 가장자리 어둠 (알파), 방사 속도선 (더하기, 회전), 위아래 검은 띠 (시네마 바), 사선 띠 셋 (멈춘 화면을 가르는 띠, 빛줄)
	/// - 배경만 바꾸는 효과는 UltimateGradeLayer. 이 층은 인물까지 덮으므로 섬광 알파는 UltimateFx 의 상한을 거쳐 들어옴
	/// - 값은 UltimateFx 가 매 프레임 넣음. 이 클래스는 그리기만
	/// </summary>
	public sealed class UltimateScreenLayer
	{
		private readonly GameObject root;
		private readonly Image flash;
		private readonly Image wash;
		private readonly Image vignette;
		private readonly Image streaks;
		private readonly RectTransform barTop;
		private readonly RectTransform barBottom;
		private readonly Image[] bands = new Image[BAND_COUNT];
		private readonly Material additive;
		private readonly Material alpha;
		private readonly Sprite whiteSprite;
		private readonly Sprite vignetteSprite;
		private readonly Sprite streakSprite;

		public const int BAND_COUNT = 3;

		public UltimateScreenLayer(Shader shader, int sortingOrder)
		{
			additive = UltimateFxStage.CreateMaterial(shader, UltimateFxTextures.White, true, Color.white);
			alpha = UltimateFxStage.CreateMaterial(shader, UltimateFxTextures.White, false, Color.white);
			whiteSprite = ToSprite(UltimateFxTextures.White);
			vignetteSprite = ToSprite(UltimateFxTextures.Vignette);
			streakSprite = ToSprite(UltimateFxTextures.Streaks);

			root = new GameObject("UltimateScreenLayer", typeof(Canvas));
			Canvas canvas = root.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;

			wash = CreateImage("Wash", whiteSprite, additive);
			vignette = CreateImage("Vignette", vignetteSprite, alpha);
			streaks = CreateImage("Streaks", streakSprite, additive);
			for (int i = 0; i < BAND_COUNT; i++)
			{
				bands[i] = CreateImage("Band" + i, whiteSprite, alpha);
				RectTransform bandRect = bands[i].rectTransform;
				bandRect.anchorMin = bandRect.anchorMax = new Vector2(0.5f, 0.5f);
				bands[i].color = Color.clear;
			}
			flash = CreateImage("Flash", whiteSprite, additive);
			barTop = CreateBar("BarTop");
			barBottom = CreateBar("BarBottom");
			SetFlash(0f, Color.white);
			SetWash(0f, Color.white);
			SetVignette(0f, Color.black);
			SetStreaks(0f, Color.white, 0f);
			SetBars(0f);
		}

		public void SetFlash(float amount, Color color) => flash.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(amount));

		public void SetWash(float amount, Color color) => wash.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(amount));

		public void SetVignette(float amount, Color color) => vignette.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(amount));

		/// <summary> 속도선. scale 은 화면을 덮는 크기 배율 (1.0 이 화면 대각선), angle 은 회전 도 </summary>
		public void SetStreaks(float amount, Color color, float angle, float scale = 1.6f)
		{
			streaks.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(amount));
			RectTransform rect = streaks.rectTransform;
			rect.localRotation = Quaternion.Euler(0f, 0f, angle);
			rect.localScale = new Vector3(scale, scale, 1f);
		}

		/// <summary>
		/// 화면을 가로지르는 사선 띠. angle 은 도, offset 과 thickness 는 화면 높이 비율. additive 면 빛줄, 아니면 덮는 띠
		/// </summary>
		public void SetBand(int index, float amount, Color color, float angle, float offset, float thickness, bool additiveBand)
		{
			Image band = bands[index];
			band.material = additiveBand ? additive : alpha;
			band.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(amount));
			RectTransform rect = band.rectTransform;
			Rect canvasRect = ((RectTransform)root.transform).rect;
			float height = canvasRect.height;
			float diagonal = Mathf.Sqrt(canvasRect.width * canvasRect.width + height * height);
			rect.sizeDelta = new Vector2(diagonal * 1.2f, Mathf.Max(0f, thickness) * height);
			rect.localRotation = Quaternion.Euler(0f, 0f, angle);
			float radians = (angle + 90f) * Mathf.Deg2Rad;
			rect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * offset * height;
		}

		/// <summary> 위아래 검은 띠. amount 0 이면 숨김, 1 이면 높이 비율 barRatio 까지 </summary>
		public void SetBars(float amount, float barRatio = 0.12f)
		{
			float ratio = Mathf.Clamp01(amount) * barRatio;
			barTop.anchorMin = new Vector2(0f, 1f - ratio);
			barTop.anchorMax = Vector2.one;
			barBottom.anchorMin = Vector2.zero;
			barBottom.anchorMax = new Vector2(1f, ratio);
			barTop.offsetMin = barTop.offsetMax = Vector2.zero;
			barBottom.offsetMin = barBottom.offsetMax = Vector2.zero;
		}

		public void Dispose()
		{
			if (root != null)
				Object.Destroy(root);
			Object.Destroy(additive);
			Object.Destroy(alpha);
			Object.Destroy(whiteSprite);
			Object.Destroy(vignetteSprite);
			Object.Destroy(streakSprite);
		}

		private static Sprite ToSprite(Texture2D texture) => Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);

		private Image CreateImage(string name, Sprite sprite, Material material)
		{
			GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
			RectTransform rect = (RectTransform)imageObject.transform;
			rect.SetParent(root.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			Image image = imageObject.GetComponent<Image>();
			image.raycastTarget = false;
			image.sprite = sprite;
			image.material = material;
			return image;
		}

		private RectTransform CreateBar(string name)
		{
			Image bar = CreateImage(name, whiteSprite, alpha);
			bar.color = Color.black;
			return bar.rectTransform;
		}
	}
}
