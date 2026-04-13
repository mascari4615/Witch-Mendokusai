using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UISpeechBubbleItem : MonoBehaviour
	{
		[SerializeField] private CanvasGroup canvasGroup;
		[SerializeField] private TextMeshProUGUI lineText;
		[SerializeField] private Image emojiImage;

		private RectTransform rectTransform;
		private Action onDone;

		private const float FadeInDuration = 0.2f;
		private const float FadeOutDuration = 0.3f;
		private const float ScreenPadding = 30f;

		private void Awake()
		{
			rectTransform = canvasGroup.GetComponent<RectTransform>();
		}

		public void Show(Transform target, string text, float duration, Action onDone)
		{
			lineText.text = text;
			lineText.gameObject.SetActive(true);
			emojiImage.gameObject.SetActive(false);
			this.onDone = onDone;
			gameObject.SetActive(true);
			StartCoroutine(ShowCoroutine(target, duration));
		}

		public void Show(Transform target, Sprite emoji, float duration, Action onDone)
		{
			emojiImage.sprite = emoji;
			emojiImage.gameObject.SetActive(true);
			lineText.gameObject.SetActive(false);
			this.onDone = onDone;
			gameObject.SetActive(true);
			StartCoroutine(ShowCoroutine(target, duration));
		}

		private IEnumerator ShowCoroutine(Transform target, float duration)
		{
			canvasGroup.alpha = 0f;
			canvasGroup.DOFade(1f, FadeInDuration);

			float elapsed = 0f;
			bool fadingOut = false;

			while (elapsed < duration + FadeOutDuration)
			{
				yield return new WaitForEndOfFrame();

				if (target != null)
					transform.position = GetScreenPos(target.position);

				elapsed += Time.deltaTime;

				if (!fadingOut && elapsed >= duration)
				{
					fadingOut = true;
					canvasGroup.DOFade(0f, FadeOutDuration);
				}
			}

			gameObject.SetActive(false);
			onDone?.Invoke();
		}

		private Vector3 GetScreenPos(Vector3 worldPos)
		{
			float halfW = rectTransform.sizeDelta.x / 2f;
			float halfH = rectTransform.sizeDelta.y / 2f;

			Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 1.5f);

			return new Vector3(
				Mathf.Clamp(screenPos.x, halfW + ScreenPadding, Screen.width - halfW - ScreenPadding),
				Mathf.Clamp(screenPos.y, ScreenPadding, Screen.height - halfH - ScreenPadding),
				0f
			);
		}
	}
}
