using System;
using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 첫 상태. 풍경과 로고, 깜빡이는 안내 한 줄만 보이고 아무 키를 누르면 메뉴가 떠오름
	public class TitleIntro : MonoBehaviour
	{
		[SerializeField] private CanvasGroup menuGroup;
		[SerializeField] private TextMeshProUGUI pressText;
		[SerializeField] private float blinkPeriod = 2.4f;
		[SerializeField, Range(0f, 1f)] private float blinkMinAlpha = 0.35f;
		[SerializeField] private float revealDuration = 0.45f;
		[Tooltip("메뉴가 떠오를 때 아래에서 올라오는 거리")]
		[SerializeField] private float revealRise = 24f;

		private IDisposable anyButtonListener;
		private bool revealed;
		private float revealStartTime;
		private Vector2 menuBasePosition;
		private RectTransform menuRect;

		private void Start()
		{
			menuRect = (RectTransform)menuGroup.transform;
			menuBasePosition = menuRect.anchoredPosition;
			SetMenuVisible(0f);
			anyButtonListener = InputManager.Instance.ListenAnyButtonOnce(Reveal);
		}

		private void OnDestroy() => anyButtonListener?.Dispose();

		private void Update()
		{
			if (revealed == false)
			{
				float wave = (Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / blinkPeriod) + 1f) * 0.5f;
				pressText.alpha = Mathf.Lerp(blinkMinAlpha, 1f, wave);
				return;
			}
			float progress = revealDuration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - revealStartTime) / revealDuration);
			float eased = 1f - (1f - progress) * (1f - progress);
			pressText.alpha = 1f - eased;
			SetMenuVisible(eased);
			if (progress >= 1f)
				enabled = false;
		}

		private void Reveal()
		{
			if (revealed)
				return;
			revealed = true;
			revealStartTime = Time.unscaledTime;
			menuGroup.interactable = true;
			menuGroup.blocksRaycasts = true;
		}

		private void SetMenuVisible(float amount)
		{
			menuGroup.alpha = amount;
			menuRect.anchoredPosition = menuBasePosition + Vector2.down * revealRise * (1f - amount);
			if (revealed == false)
			{
				menuGroup.interactable = false;
				menuGroup.blocksRaycasts = false;
			}
		}
	}
}
