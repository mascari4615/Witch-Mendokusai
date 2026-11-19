using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 동안 화면 위에 덮는 층. 전부 화면 위 이미지 (후처리 Volume 은 이 카메라에서 반영되지 않음)
	/// - 섬광 (더하기), 색 번짐 (더하기), 가장자리 어둠 (알파), 방사 속도선 (더하기, 회전), 위아래 검은 띠 (시네마 바)
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
		private readonly Material additive;
		private readonly Material alpha;
		private readonly Sprite whiteSprite;
		private readonly Sprite vignetteSprite;
		private readonly Sprite streakSprite;

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
			flash = CreateImage("Flash", whiteSprite, additive);
			barTop = CreateBar("BarTop", true);
			barBottom = CreateBar("BarBottom", false);
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

		private RectTransform CreateBar(string name, bool top)
		{
			Image bar = CreateImage(name, whiteSprite, alpha);
			bar.color = Color.black;
			return bar.rectTransform;
		}
	}
}
