using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 형태 카드 (갈래 확정, 최종) 의 얼굴. 일반 카드 칸을 그대로 두고 둘레의 가는 빛, 그림 칸의 흐르는 빛, 아이콘 하나만 얹음. 값은 FormCardLook
	// - 공개: 전용 뒷면 (CardRevealEntry.Back) 으로 나와 이 한 장만 다르다는 것을 먼저 알림. 둘레 빛은 공개가 끝난 뒤
	// - 고른 뒤: 카드가 희게 번쩍이고 세로 빛이 섬 (이어서 변신)
	// 카드 칸은 다시 쓰이므로 일반 카드가 오면 Hide
	public sealed class UIFormCardFace : MonoBehaviour
	{
		private static readonly int DARK_ID = Shader.PropertyToID("_Dark");
		private static readonly int MID_ID = Shader.PropertyToID("_Mid");
		private static readonly int MAIN_ID = Shader.PropertyToID("_Main");
		private static readonly int BRIGHT_ID = Shader.PropertyToID("_Bright");
		private static readonly int SIZE_ID = Shader.PropertyToID("_Size");
		private static readonly int MARGIN_ID = Shader.PropertyToID("_Margin");
		private static readonly int MODE_ID = Shader.PropertyToID("_Mode");

		private enum Mode
		{
			Art = 0,
			Border = 1,
			Pillar = 2,
		}

		private readonly List<Object> owned = new();
		private FormCardLook look;
		private RectTransform front;
		private RectTransform artRoot;
		private Image border;
		private Image flash;

		private bool IsShown => look != null && front.gameObject.activeSelf;

		public void Show(FormCardLook newLook, RectTransform artArea)
		{
			if (look != newLook)
			{
				Clear();
				look = newLook;
				Build(artArea);
			}
			SetParts(true);
			flash.gameObject.SetActive(false);
			border.gameObject.SetActive(UIEffects.Enabled == false);
		}

		public void Hide()
		{
			if (look != null)
				SetParts(false);
		}

		// 공개 연출이 끝난 뒤 (연출이 없으면 바로) 둘레 빛을 켬
		public void Reveal()
		{
			if (IsShown && border.gameObject.activeSelf == false)
				border.gameObject.SetActive(true);
		}

		// 고른 순간. 카드는 덱으로 날아가므로 세로 빛은 canvas 에 둠
		public void PlayPicked(Canvas canvas)
		{
			if (IsShown == false)
				return;
			RectTransform card = (RectTransform)transform;
			flash.gameObject.SetActive(true);
			flash.color = look.Bright;
			flash.DOFade(0f, look.PickFlashSeconds).SetUpdate(true).SetLink(flash.gameObject);
			Material pillarMaterial = Own(Light(look.PillarSize, Mode.Pillar));
			Image pillar = NewImage("[FormCard] Pillar", canvas.transform, pillarMaterial);
			RectTransform rect = pillar.rectTransform;
			rect.sizeDelta = look.PillarSize;
			rect.pivot = new Vector2(0.5f, 0.1f);
			rect.position = card.TransformPoint(new Vector2(card.rect.center.x, card.rect.yMin));
			rect.localScale = new Vector3(0.05f, 1f, 1f);
			DOTween.Sequence().SetUpdate(true).SetLink(pillar.gameObject)
				.Append(rect.DOScaleX(1f, look.PillarSeconds * 0.2f).SetEase(Ease.OutCubic))
				.Append(rect.DOScaleX(0f, look.PillarSeconds * 0.8f).SetEase(Ease.InQuad))
				.Join(pillar.DOFade(0f, look.PillarSeconds * 0.8f).SetEase(Ease.InQuad))
				.OnComplete(() =>
				{
					owned.Remove(pillarMaterial);
					Destroy(pillarMaterial);
					Destroy(pillar.gameObject);
				});
		}

		private void Update()
		{
			if (IsShown)
				UIEffects.TickShaderTime();
		}

		private void OnDestroy() => Clear();

		private void SetParts(bool visible)
		{
			front.gameObject.SetActive(visible);
			artRoot.gameObject.SetActive(visible);
		}

		private void Build(RectTransform artArea)
		{
			RectTransform card = (RectTransform)transform;
			artRoot = NewRect("[FormCard] Art", artArea);
			Image light = NewImage("Light", artRoot, Own(Light(artArea.rect.size, Mode.Art)));
			Stretch(light.rectTransform);
			AddIcon(artRoot, Vector2.zero, look.IconSize);

			front = NewRect("[FormCard] Front", card);
			front.SetAsLastSibling();
			Vector2 outer = card.rect.size + Vector2.one * (look.BorderMargin * 2f);
			Material borderMaterial = Own(Light(outer, Mode.Border));
			borderMaterial.SetFloat(MARGIN_ID, look.BorderMargin);
			border = NewImage("Border", front, borderMaterial);
			border.rectTransform.sizeDelta = outer;
			flash = NewImage("Flash", front, null);
			Stretch(flash.rectTransform);
		}

		// 흰 아이콘과 어두운 그림자
		private void AddIcon(RectTransform parent, Vector2 position, float size)
		{
			AddIcon(parent, position + look.IconShadow, size, new Color(look.Dark.r, look.Dark.g, look.Dark.b, look.IconShadowAlpha));
			AddIcon(parent, position, size, look.Bright);
		}

		private void AddIcon(RectTransform parent, Vector2 position, float size, Color color)
		{
			Image icon = NewImage("Icon", parent, null);
			icon.sprite = look.Icon;
			icon.preserveAspect = true;
			icon.color = color;
			icon.rectTransform.sizeDelta = Vector2.one * size;
			icon.rectTransform.anchoredPosition = position;
		}

		private Material Light(Vector2 size, Mode mode)
		{
			Material material = new(look.LightShader) { name = "FormCardLight" };
			material.SetColor(DARK_ID, look.Dark);
			material.SetColor(MID_ID, look.Mid);
			material.SetColor(MAIN_ID, look.Main);
			material.SetColor(BRIGHT_ID, look.Bright);
			material.SetVector(SIZE_ID, size);
			material.SetFloat(MODE_ID, (float)mode);
			return material;
		}

		private T Own<T>(T target) where T : Object
		{
			owned.Add(target);
			return target;
		}

		private void Clear()
		{
			foreach (Object target in owned)
				Destroy(target);
			owned.Clear();
			if (look == null)
				return;
			Destroy(front.gameObject);
			Destroy(artRoot.gameObject);
			look = null;
		}

		private static RectTransform NewRect(string name, Transform parent)
		{
			RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
			rect.SetParent(parent, false);
			Stretch(rect);
			return rect;
		}

		private static Image NewImage(string name, Transform parent, Material material)
		{
			Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
			image.transform.SetParent(parent, false);
			image.raycastTarget = false;
			if (material != null)
				image.material = material;
			return image;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
	}
}
