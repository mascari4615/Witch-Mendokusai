using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 공개 중인 칸 하나의 꾸밈. 뒷면, 뒤 오라, 광택 훑기, 어둡게
	// - 뒷면은 칸 맨 앞에 덮어 앞면을 가림. 뒤집기 절반에서 끔
	// - 위치, 회전, 크기는 시작 값을 기억해 끝나면 정확히 되돌림
	public sealed class CardRevealCard
	{
		private static readonly Color BACK_FRAME = new(0.55f, 0.47f, 0.27f, 1f);
		private static readonly Color BACK_FILL = new(0.11f, 0.1f, 0.15f, 1f);
		private static readonly Color BACK_INNER = new(0.08f, 0.07f, 0.11f, 1f);
		private static readonly Color SHADE = new(0f, 0f, 0f, 0.55f);
		private const float BACK_BORDER = 5f;
		private const float EMBLEM_RATIO = 0.36f;
		private const float SHINE_WIDTH_RATIO = 0.42f;

		private readonly RectTransform card;
		private readonly CanvasGroup group;
		private Vector3 homePosition;
		private Quaternion homeRotation;
		private Vector3 homeScale;
		private GameObject back;
		private Image flipEdge;
		private const int EffectColumns = 6;
		private const int EffectRows = 4;
		private const float FlipPitch = 13f;
		private const float FlipRoll = 6f;
		private Image aura;
		private RectTransform shine;
		private Image shade;
		private Tween auraIdle;
		private bool prepared;

		public RectTransform Rect => card;
		public CardRevealTier Tier { get; }
		// 등급 색을 대신하는 빛 (형태 카드)
		public Color? Tint { get; }
		public Vector3 HomePosition => homePosition;
		public Quaternion HomeRotation => homeRotation;
		public Vector3 HomeScale => homeScale;

		public CardRevealCard(CardRevealEntry entry)
		{
			card = entry.Card;
			Tier = entry.Tier;
			Tint = entry.Tint;
			if (card.TryGetComponent(out group) == false)
				group = card.gameObject.AddComponent<CanvasGroup>();
		}

		// 시작 전 상태 기억, 뒷면 덮기, 숨김
		// backSprite 가 있으면 그 그림 뒷면, 없으면 도형 뒷면
		public void Prepare(Color tierColor, float auraSizeRatio, float shineAngle, Sprite backSprite = null)
		{
			Dispose();
			homePosition = card.localPosition;
			homeRotation = card.localRotation;
			homeScale = card.localScale;
			Vector2 size = card.rect.size;
			back = backSprite != null ? NewImage("[Reveal] Back", backSprite, Color.white, size).gameObject : BuildBack(size);
			aura = NewImage("[Reveal] Aura", RevealTextures.Ring, new Color(tierColor.r, tierColor.g, tierColor.b, 0f), size * auraSizeRatio);
			aura.transform.SetAsFirstSibling();
			shade = NewImage("[Reveal] Shade", null, new Color(SHADE.r, SHADE.g, SHADE.b, 0f), size);
			shine = BuildShine(size, shineAngle);
			back.transform.SetAsLastSibling();
			// 영웅 감기 때 뒷면 카드도 어두워지게 맨 앞
			shade.transform.SetAsLastSibling();
			group.alpha = 0f;
			// 숨긴 카드에 마우스가 닿아 선택, 툴팁, 들림이 새지 않게
			group.blocksRaycasts = false;
			prepared = true;
		}

		public CanvasGroup Group => group;
		public Image Aura => aura;
		public RectTransform Shine => shine;
		public Image Shade => shade;

		// 뒤집기 절반. 뒷면을 끄고 앞면
		public void ShowFront()
		{
			if (back != null)
				back.SetActive(false);
		}

		public Tween BackEffect(Texture2D atlas, float duration)
		{
			GameObject overlay = new("[Reveal] Back effect", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
			RawImage image = overlay.GetComponent<RawImage>();
			image.texture = atlas;
			image.raycastTarget = false;
			RectTransform rect = image.rectTransform;
			rect.SetParent(back.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			image.uvRect = EffectFrame(0);
			return DOVirtual.Float(0f, 1f, duration, progress =>
			{
				int index = Mathf.Min(EffectColumns * EffectRows - 1, (int)(progress * EffectColumns * EffectRows));
				image.uvRect = EffectFrame(index);
			}).SetEase(Ease.Linear);
		}

		private static Rect EffectFrame(int index)
		{
			const float Width = 1f / EffectColumns;
			const float Height = 1f / EffectRows;
			return new Rect(index % EffectColumns * Width, 1f - (index / EffectColumns + 1) * Height, Width, Height);
		}

		public void BuildFlipEdge(Color tint)
		{
			Vector2 size = card.rect.size;
			flipEdge = NewImage("[Reveal] Card stock edge", null, tint, new Vector2(size.x * 0.023f, size.y));
			flipEdge.rectTransform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			flipEdge.gameObject.SetActive(false);
			shade.transform.SetAsLastSibling();
		}

		public void SetFlipPose(float turn)
		{
			bool front = turn >= 0.5f;
			float angle = turn * 180f - (front ? 180f : 0f);
			float lift = Mathf.Sin(turn * Mathf.PI);
			Quaternion turnAxis = Quaternion.AngleAxis(angle, Vector3.up);
			Quaternion tilt = Quaternion.AngleAxis(-FlipPitch * lift, Vector3.right)
				* Quaternion.AngleAxis(FlipRoll * lift, Vector3.forward);
			// 두 면에서 기울기의 기준축을 공유해야 90도에서 옆면 방향이 뒤집히지 않음
			card.localRotation = homeRotation * tilt * turnAxis;
			flipEdge.gameObject.SetActive(turn > 0.22f && turn < 0.78f);
			flipEdge.rectTransform.localPosition = new Vector3(card.rect.width * (front ? -0.5f : 0.5f), 0f, 0f);
			if (front)
				ShowFront();
		}

		// 광택이 왼쪽에서 오른쪽으로 한 번
		public Tween Sweep(float duration)
		{
			float width = card.rect.width;
			shine.gameObject.SetActive(true);
			RectTransform band = (RectTransform)shine.GetChild(0);
			band.anchoredPosition = new Vector2(-width, 0f);
			return band.DOAnchorPosX(width, duration).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() => shine.gameObject.SetActive(false));
		}

		// 끝 상태. 앞면, 제자리, 영웅이면 오라가 계속 숨쉼
		public void Finish(float idleAlpha, float idlePeriod)
		{
			if (prepared == false)
				return;
			card.DOKill();
			card.localPosition = homePosition;
			card.localRotation = homeRotation;
			card.localScale = homeScale;
			group.alpha = 1f;
			group.blocksRaycasts = true;
			if (back != null)
				Object.Destroy(back);
			if (flipEdge != null)
				Object.Destroy(flipEdge.gameObject);
			flipEdge = null;
			if (shine != null)
			{
				shine.GetChild(0).DOKill();
				Object.Destroy(shine.gameObject);
			}
			if (shade != null)
				Object.Destroy(shade.gameObject);
			back = null;
			shine = null;
			shade = null;
			if (aura == null)
				return;
			aura.DOKill();
			if (Tier == CardRevealTier.Common)
			{
				Object.Destroy(aura.gameObject);
				aura = null;
				return;
			}
			Color color = aura.color;
			aura.color = new Color(color.r, color.g, color.b, idleAlpha);
			aura.transform.localScale = Vector3.one;
			if (Tier >= CardRevealTier.Epic)
				auraIdle = aura.DOFade(idleAlpha * 0.55f, idlePeriod * 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
		}

		// 패널 닫힘, 다음 공개 전. 꾸밈 전부 제거
		public void Dispose()
		{
			if (card == null)
				return;
			Finish(0f, 1f);
			auraIdle?.Kill();
			auraIdle = null;
			if (aura != null)
				Object.Destroy(aura.gameObject);
			aura = null;
			prepared = false;
		}

		private Image NewImage(string objectName, Sprite sprite, Color color, Vector2 size)
		{
			GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
			RectTransform rect = (RectTransform)imageObject.transform;
			rect.SetParent(card, false);
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = Vector2.zero;
			rect.sizeDelta = size;
			imageObject.GetComponent<LayoutElement>().ignoreLayout = true;
			Image image = imageObject.GetComponent<Image>();
			image.sprite = sprite;
			image.color = color;
			image.raycastTarget = false;
			return image;
		}

		// 금색 테두리, 어두운 안, 가운데 마름모 문양
		private GameObject BuildBack(Vector2 size)
		{
			Image frame = NewImage("[Reveal] Back", null, BACK_FRAME, size);
			Image fill = NewChild(frame.rectTransform, BACK_FILL, size - Vector2.one * BACK_BORDER * 2f, 0f);
			float emblem = Mathf.Min(size.x, size.y) * EMBLEM_RATIO;
			NewChild(fill.rectTransform, BACK_FRAME, Vector2.one * emblem, 45f);
			NewChild(fill.rectTransform, BACK_INNER, Vector2.one * emblem * 0.78f, 45f);
			NewChild(fill.rectTransform, BACK_FRAME, Vector2.one * emblem * 0.3f, 45f);
			return frame.gameObject;
		}

		private RectTransform BuildShine(Vector2 size, float shineAngle)
		{
			Image mask = NewImage("[Reveal] Shine", null, Color.clear, size);
			mask.gameObject.AddComponent<RectMask2D>();
			Image band = NewChild(mask.rectTransform, new Color(1f, 1f, 1f, 0.5f), new Vector2(size.x * SHINE_WIDTH_RATIO, size.y * 1.6f), shineAngle);
			band.sprite = RevealTextures.Beam;
			mask.gameObject.SetActive(false);
			return mask.rectTransform;
		}

		private static Image NewChild(RectTransform parent, Color color, Vector2 size, float angle)
		{
			GameObject child = new("[Image] Part", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform rect = (RectTransform)child.transform;
			rect.SetParent(parent, false);
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = size;
			rect.localRotation = Quaternion.Euler(0f, 0f, angle);
			Image image = child.GetComponent<Image>();
			image.color = color;
			image.raycastTarget = false;
			return image;
		}
	}
}
