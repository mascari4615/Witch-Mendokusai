using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드 한 장의 슬롯 회전. 회전 중 그림과 이름을 후보로 순환, 멈추면 원래 값 복구 뒤 튕김과 번쩍
	public sealed class CardReel
	{
		private readonly CardRevealEntry entry;
		private readonly CardRevealStyle style;
		private readonly List<CardRevealSymbol> symbols;
		private readonly List<CanvasGroup> detailGroups = new();
		private readonly Sprite finalSprite;
		private readonly string finalName;
		private readonly float finalFontSize;
		private readonly Color finalIconColor;
		private readonly Color finalLabelColor;
		private readonly Vector2 iconPosition;
		private readonly Vector2 labelPosition;
		private readonly Vector3 iconScale;
		private readonly Vector3 labelScale;
		private readonly float stopAt;
		private readonly UISlotRays rays;
		private int symbolIndex;
		private float stepStartedAt;
		private float stepEndsAt;
		private float rayStartedAt = -1f;

		public bool IsSpinning { get; private set; }
		public bool IsAlive => entry.Icon != null && entry.Label != null && entry.PunchTarget != null;
		public CardRevealTier Tier => entry.Tier;

		public CardReel(CardRevealEntry entry, CardRevealStyle style, List<CardRevealSymbol> symbols, int symbolOffset, float startTime, float stopAt, UISlotRays rays)
		{
			this.entry = entry;
			this.style = style;
			this.symbols = symbols;
			this.stopAt = stopAt;
			this.rays = rays;
			finalSprite = entry.Icon.sprite;
			finalName = entry.Label.text;
			finalFontSize = entry.Label.fontSize;
			finalIconColor = entry.Icon.color;
			finalLabelColor = entry.Label.color;
			iconPosition = entry.Icon.rectTransform.anchoredPosition;
			labelPosition = entry.Label.rectTransform.anchoredPosition;
			iconScale = entry.Icon.rectTransform.localScale;
			labelScale = entry.Label.rectTransform.localScale;
			foreach (GameObject detail in entry.Details)
			{
				if (detail.TryGetComponent(out CanvasGroup group) == false)
					group = detail.AddComponent<CanvasGroup>();
				group.DOKill();
				group.alpha = 0f;
				detailGroups.Add(group);
			}
			if (rays != null)
				rays.gameObject.SetActive(false);
			symbolIndex = symbolOffset;
			stepStartedAt = startTime;
			stepEndsAt = startTime + style.SpinStepInterval;
			IsSpinning = true;
			ShowSymbol();
		}

		public void Tick(float now)
		{
			if (IsSpinning)
			{
				if (now >= stopAt)
				{
					Stop(true, true);
					return;
				}
				while (now >= stepEndsAt)
				{
					symbolIndex++;
					stepStartedAt = stepEndsAt;
					stepEndsAt = stepStartedAt + CurrentStepInterval(stepStartedAt);
					ShowSymbol();
				}
				float phase = Mathf.InverseLerp(stepStartedAt, stepEndsAt, now);
				float offset = Mathf.Lerp(-style.ScrollDistance, style.ScrollDistance, phase);
				entry.Icon.rectTransform.anchoredPosition = iconPosition + new Vector2(0f, offset);
				entry.Label.rectTransform.anchoredPosition = labelPosition + new Vector2(0f, offset);
				return;
			}
			TickRays(now);
		}

		// 멈춤. effects 가 false 면 연출 없이 값만 복구 (패널 닫힘, 다시 뽑기)
		public void Stop(bool effects, bool playSound)
		{
			if (IsSpinning == false)
				return;
			IsSpinning = false;
			RestoreFinal();
			if (effects == false)
			{
				foreach (CanvasGroup group in detailGroups)
					group.alpha = 1f;
				return;
			}

			float settle = style.ScrollDistance * style.StopSettleRatio;
			entry.Icon.rectTransform.anchoredPosition = iconPosition - new Vector2(0f, settle);
			entry.Label.rectTransform.anchoredPosition = labelPosition - new Vector2(0f, settle);
			entry.Icon.rectTransform.DOAnchorPos(iconPosition, style.StopSettleDuration).SetEase(Ease.OutBack).SetUpdate(true);
			entry.Label.rectTransform.DOAnchorPos(labelPosition, style.StopSettleDuration).SetEase(Ease.OutBack).SetUpdate(true);

			Transform punchTarget = entry.PunchTarget;
			punchTarget.DOKill();
			punchTarget.localScale = Vector3.one * style.TierPunch(entry.Tier);
			punchTarget.DOScale(Vector3.one, style.StopPunchDuration).SetEase(Ease.OutBack).SetUpdate(true);

			foreach (CanvasGroup group in detailGroups)
				group.DOFade(1f, style.DetailFadeDuration).SetUpdate(true);

			Color tierColor = style.TierColor(entry.Tier);
			if (entry.ChoiceCard != null)
			{
				entry.ChoiceCard.PlayRevealFlash(tierColor, style.FlashDuration);
				int burstCount = style.TierBurst(entry.Tier);
				if (burstCount > 0)
					entry.ChoiceCard.PlayRevealBurst(tierColor, burstCount);
			}
			if (entry.Tier != CardRevealTier.Common && rays != null)
			{
				rays.Configure(style.RayCount, style.RayWidth, style.RayRotationSpeed);
				float size = Mathf.Max(entry.Icon.rectTransform.rect.width, entry.Icon.rectTransform.rect.height) * style.RayRadiusRatio * 2f;
				rays.rectTransform.sizeDelta = new Vector2(size, size);
				rays.transform.position = entry.Icon.transform.parent.TransformPoint(entry.Icon.rectTransform.localPosition + new Vector3(0f, settle, 0f));
				rays.gameObject.SetActive(true);
				rays.color = Color.clear;
				rays.transform.localScale = Vector3.one * style.RayPopScale;
				rays.transform.DOKill();
				rays.transform.DOScale(Vector3.one, style.RayDecayTime).SetEase(Ease.OutQuad).SetUpdate(true);
				rayStartedAt = Time.unscaledTime;
			}
			if (playSound)
				UiSfx.Play(style.TierSfx(entry.Tier));
		}

		// 회전 중이든 멈춤 연출 중이든 즉시 최종 모습으로. 다시 뽑기와 패널 닫힘
		public void ResetInstant()
		{
			IsSpinning = false;
			RestoreFinal();
			entry.PunchTarget.DOKill(true);
			entry.PunchTarget.localScale = Vector3.one;
			foreach (CanvasGroup group in detailGroups)
			{
				group.DOKill();
				group.alpha = 1f;
			}
		}

		public void HideRays()
		{
			rayStartedAt = -1f;
			if (rays != null)
			{
				rays.transform.DOKill();
				rays.gameObject.SetActive(false);
			}
		}

		private void TickRays(float now)
		{
			if (rayStartedAt < 0f || rays == null)
				return;
			float age = now - rayStartedAt;
			// 잔광 밝기에 닿은 뒤는 회전만, 매 프레임 메시 갱신 생략
			bool settled = age >= style.RayPeakTime + style.RayDecayTime;
			float alpha = age < style.RayPeakTime
				? Mathf.Lerp(0f, style.RayPeakAlpha, age / style.RayPeakTime)
				: Mathf.Lerp(style.RayPeakAlpha, style.RayIdleAlpha, (age - style.RayPeakTime) / style.RayDecayTime);
			if (entry.Tier == CardRevealTier.Rare)
				alpha *= style.RareRayAlphaScale;
			Color tierColor = style.TierColor(entry.Tier);
			rays.color = new Color(tierColor.r, tierColor.g, tierColor.b, alpha);
			if (settled)
				rayStartedAt = -1f;
		}

		private float CurrentStepInterval(float time)
		{
			if (style.SlowdownTime <= 0f)
				return style.SpinStepInterval;
			float closeness = Mathf.Clamp01(1f - (stopAt - time) / style.SlowdownTime);
			return Mathf.Lerp(style.SpinStepInterval, style.SlowStepInterval, closeness * closeness);
		}

		private void ShowSymbol()
		{
			if (symbols.Count > 0)
			{
				CardRevealSymbol symbol = symbols[symbolIndex % symbols.Count];
				entry.Icon.sprite = symbol.Sprite;
				entry.Label.text = symbol.Name;
			}
			entry.Icon.color = new Color(finalIconColor.r, finalIconColor.g, finalIconColor.b, finalIconColor.a * style.SpinAlpha);
			entry.Label.color = new Color(finalLabelColor.r, finalLabelColor.g, finalLabelColor.b, finalLabelColor.a * style.SpinAlpha);
			entry.Icon.rectTransform.localScale = new Vector3(iconScale.x, iconScale.y * style.SpinStretch, iconScale.z);
			entry.Label.rectTransform.localScale = new Vector3(labelScale.x, labelScale.y * style.SpinStretch, labelScale.z);
		}

		private void RestoreFinal()
		{
			entry.Icon.rectTransform.DOKill();
			entry.Label.rectTransform.DOKill();
			entry.Icon.sprite = finalSprite;
			entry.Icon.color = finalIconColor;
			entry.Label.text = finalName;
			entry.Label.fontSize = finalFontSize;
			entry.Label.color = finalLabelColor;
			entry.Icon.rectTransform.anchoredPosition = iconPosition;
			entry.Label.rectTransform.anchoredPosition = labelPosition;
			entry.Icon.rectTransform.localScale = iconScale;
			entry.Label.rectTransform.localScale = labelScale;
		}
	}
}
