using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 상자 개봉 예고. 흔들림과 새어 나오는 빛 뒤 번쩍임, 그다음 보상 칸 슬롯 회전으로 넘김
	public sealed class UIChestOpenEffect : MonoBehaviour
	{
		private RectTransform chest;
		private Image chestImage;
		private Color chestColor;
		private UISlotRays leak;
		private Image flash;
		private CardRevealStyle style;
		private Color tierColor;
		private Action<bool> onOpen = delegate { };
		private float startedAt;
		private int startedFrame;
		private float nextRattleAt;

		public bool IsPlaying { get; private set; }

		// stage 는 상자 자리 부모, flashParent 는 화면 전체 번쩍임 부모
		public static UIChestOpenEffect Create(RectTransform stage, Vector3 localPosition, RectTransform flashParent, Sprite chestSprite)
		{
			UIChestOpenEffect effect = stage.gameObject.AddComponent<UIChestOpenEffect>();
			effect.leak = UISlotRays.Create(stage, stage.childCount);
			effect.leak.rectTransform.localPosition = localPosition;

			GameObject chestObject = new("[Image] RevealChest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			effect.chest = (RectTransform)chestObject.transform;
			effect.chest.SetParent(stage, false);
			effect.chest.anchorMin = effect.chest.anchorMax = new Vector2(0.5f, 0.5f);
			effect.chest.localPosition = localPosition;
			effect.chestImage = chestObject.GetComponent<Image>();
			effect.chestImage.sprite = chestSprite;
			effect.chestColor = effect.chestImage.color;
			effect.chestImage.preserveAspect = true;
			effect.chestImage.raycastTarget = false;
			chestObject.SetActive(false);

			GameObject flashObject = new("[Image] RevealFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform flashRect = (RectTransform)flashObject.transform;
			flashRect.SetParent(flashParent, false);
			flashRect.anchorMin = Vector2.zero;
			flashRect.anchorMax = Vector2.one;
			flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
			effect.flash = flashObject.GetComponent<Image>();
			effect.flash.raycastTarget = false;
			flashObject.SetActive(false);
			return effect;
		}

		// opened 인자는 입력으로 건너뛰었는지
		public void Play(CardRevealStyle revealStyle, Color color, Action<bool> opened)
		{
			Cancel();
			style = revealStyle;
			tierColor = color;
			onOpen = opened;
			if (UIEffects.Enabled == false)
			{
				onOpen(false);
				return;
			}
			flash.transform.SetAsLastSibling();
			chest.gameObject.SetActive(true);
			chest.sizeDelta = Vector2.one * style.ChestIconSize;
			chest.localScale = Vector3.one;
			chest.localRotation = Quaternion.identity;
			chestImage.color = chestColor;
			leak.Configure(style.RayCount, style.RayWidth, style.RayRotationSpeed);
			leak.rectTransform.sizeDelta = Vector2.one * style.ChestIconSize * style.RayRadiusRatio * 2f;
			leak.color = Color.clear;
			leak.gameObject.SetActive(true);
			startedAt = Time.unscaledTime;
			startedFrame = Time.frameCount;
			nextRattleAt = startedAt;
			IsPlaying = true;
		}

		public void Skip()
		{
			if (IsPlaying)
				Open(true);
		}

		public void Cancel()
		{
			IsPlaying = false;
			chest.DOKill();
			chestImage.DOKill();
			flash.DOKill();
			leak.transform.DOKill();
			chest.gameObject.SetActive(false);
			leak.gameObject.SetActive(false);
			flash.gameObject.SetActive(false);
		}

		private void Update()
		{
			if (IsPlaying == false)
				return;
			if (Time.frameCount > startedFrame && InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsAnyKeyPressedThisFrame)
			{
				Open(true);
				return;
			}
			float now = Time.unscaledTime;
			float progress = Mathf.Clamp01((now - startedAt) / style.ChestShakeDuration);
			if (progress >= 1f)
			{
				Open(false);
				return;
			}
			// 끝으로 갈수록 세게 흔들림
			float strength = progress * progress;
			float wave = Mathf.Sin((now - startedAt) * style.ChestShakeFrequency * Mathf.PI * 2f);
			chest.localRotation = Quaternion.Euler(0f, 0f, wave * style.ChestShakeAngle * strength);
			chest.anchoredPosition = leak.rectTransform.anchoredPosition + new Vector2(wave * style.ChestShakeOffset * strength, 0f);
			chest.localScale = Vector3.one * Mathf.Lerp(1f, style.ChestSwellScale, strength);
			leak.color = new Color(tierColor.r, tierColor.g, tierColor.b, style.ChestLeakAlpha * strength);
			leak.transform.localScale = Vector3.one * Mathf.Lerp(style.ChestLeakStartScale, 1f, progress);
			if (now >= nextRattleAt)
			{
				UiSfx.Play(style.SpinTickSfx);
				nextRattleAt = now + style.ChestRattleInterval;
			}
		}

		private void Open(bool skipped)
		{
			IsPlaying = false;
			chest.localRotation = Quaternion.identity;
			chest.anchoredPosition = leak.rectTransform.anchoredPosition;
			chest.DOScale(Vector3.one * style.ChestPopScale, style.ChestPopDuration).SetEase(Ease.OutQuad).SetUpdate(true);
			chestImage.DOFade(0f, style.ChestPopDuration).SetUpdate(true).OnComplete(() => chest.gameObject.SetActive(false));
			leak.color = new Color(tierColor.r, tierColor.g, tierColor.b, style.RayPeakAlpha);
			leak.transform.DOScale(Vector3.one * style.RayPopScale, style.ChestPopDuration).SetUpdate(true).OnComplete(() => leak.gameObject.SetActive(false));
			flash.gameObject.SetActive(true);
			flash.DOKill();
			flash.color = new Color(tierColor.r, tierColor.g, tierColor.b, style.ChestFlashAlpha);
			flash.DOFade(0f, style.ChestFlashDuration).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() => flash.gameObject.SetActive(false));
			UiSfx.Play(style.ChestOpenSfx);
			onOpen(skipped);
		}
	}
}
