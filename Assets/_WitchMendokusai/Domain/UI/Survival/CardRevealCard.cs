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
		private const float SHINE_ANGLE = 20f;

		private readonly RectTransform card;
		private readonly CanvasGroup group;
		private Vector3 homePosition;
		private Quaternion homeRotation;
		private Vector3 homeScale;
		private GameObject back;
		private Image aura;
		private RectTransform shine;
		private Image shade;
		private Tween auraIdle;
		private bool prepared;

		public RectTransform Rect => card;
		public CardRevealTier Tier { get; }
		public Vector3 HomePosition => homePosition;
		public Quaternion HomeRotation => homeRotation;
		public Vector3 HomeScale => homeScale;

		public CardRevealCard(CardRevealEntry entry)
		{
			card = entry.Card;
			Tier = entry.Tier;
			if (card.TryGetComponent(out group) == false)
				group = card.gameObject.AddComponent<CanvasGroup>();
		}

		// 시작 전 상태 기억, 뒷면 덮기, 숨김
		public void Prepare(Color tierColor, float auraSizeRatio)
		{
			Dispose();
			homePosition = card.localPosition;
			homeRotation = card.localRotation;
			homeScale = card.localScale;
			Vector2 size = card.rect.size;
			back = BuildBack(size);
			aura = NewImage("[Reveal] Aura", RevealTextures.Ring, new Color(tierColor.r, tierColor.g, tierColor.b, 0f), size * auraSizeRatio);
			aura.transform.SetAsFirstSibling();
			shade = NewImage("[Reveal] Shade", null, new Color(SHADE.r, SHADE.g, SHADE.b, 0f), size);
			shine = BuildShine(size);
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
			if (Tier == CardRevealTier.Epic)
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

		private RectTransform BuildShine(Vector2 size)
		{
			Image mask = NewImage("[Reveal] Shine", null, Color.clear, size);
			mask.gameObject.AddComponent<RectMask2D>();
			Image band = NewChild(mask.rectTransform, new Color(1f, 1f, 1f, 0.5f), new Vector2(size.x * SHINE_WIDTH_RATIO, size.y * 1.6f), SHINE_ANGLE);
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
