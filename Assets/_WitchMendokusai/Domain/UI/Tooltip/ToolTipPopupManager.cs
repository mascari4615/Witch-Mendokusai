using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace WitchMendokusai
{
	public class ToolTipPopupManager : MonoBehaviour
	{
		public static ToolTipPopupManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out ToolTipPopupManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		[SerializeField] private ToolTip popupToolTip;
		[SerializeField] private CanvasGroup canvasGroup;

		// 툴팁이 뜨고 지는 호흡 — 눈으로 맞춰야 하는 값이라 인스펙터로 낸다(TASK-WM-108).
		// 뜨는 쪽이 지는 쪽보다 빨라야 「손이 닿으면 바로 뜬다」로 읽힌다(기본 30 : 10).
		[Header("Fade")]
		[Tooltip("나타날 때 알파가 1로 붙는 속도. 클수록 즉각적.")]
		[SerializeField] private float showLerpSpeed = 30f;
		[Tooltip("사라질 때 알파가 0으로 빠지는 속도. 작을수록 여운이 남는다.")]
		[SerializeField] private float hideLerpSpeed = 10f;
		[Tooltip("숨기라는 신호 뒤 이만큼 버틴다 — 슬롯 사이를 스칠 때 툴팁이 깜빡이지 않게.")]
		[SerializeField] private float disappearDelay = 0.3f;

		private InputManager inputManager;

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
		}

		private bool isShow;
		private float disappearTimer;

		private float toolTipWidth;
		private float toolTipHeight;
		private const float ToolTipPadding = 30f;

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;

			RectTransform rectTransform = popupToolTip.GetComponent<RectTransform>();
			toolTipWidth = rectTransform.sizeDelta.x;
			toolTipHeight = rectTransform.sizeDelta.y;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public void Show(SlotData slotData)
		{
			popupToolTip.SetToolTipContent(slotData);
			popupToolTip.transform.position = GetVec();
			isShow = true;
		}

		private void Update()
		{
			popupToolTip.transform.position = GetVec();

			if (isShow)
			{
				canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1, Time.unscaledDeltaTime * showLerpSpeed);
			}
			else
			{
				if (disappearTimer > 0)
				{
					disappearTimer -= Time.unscaledDeltaTime;
					return;
				}

				canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0, Time.unscaledDeltaTime * hideLerpSpeed);
			}
		}

		private Vector3 GetVec()
		{
			Vector2 mousePos = inputManager.MouseScreenPosition;
			return new Vector3(
				Mathf.Clamp(mousePos.x, toolTipWidth / 2 + ToolTipPadding, Screen.width - toolTipWidth / 2 - ToolTipPadding),
				Mathf.Clamp(mousePos.y + 40, ToolTipPadding, Screen.height - toolTipHeight - ToolTipPadding), 0);
		}

		public void Hide()
		{
			// Debug.Log("Hide");
			isShow = false;
			disappearTimer = disappearDelay;
		}
	}
}