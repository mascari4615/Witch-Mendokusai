using System;
using UnityEngine;

namespace WitchMendokusai
{
	public class ToolTipPopupManager : Singleton<ToolTipPopupManager>
	{
		[SerializeField] private ToolTip popupToolTip;
		[SerializeField] private CanvasGroup canvasGroup;

		private bool isShow;
		private float disappearTimer;

		private float toolTipWidth;
		private float toolTipHeight;
		private const float ToolTipPadding = 30f;

		protected override void Awake()
		{
			base.Awake();

			RectTransform rectTransform = popupToolTip.GetComponent<RectTransform>();
			toolTipWidth = rectTransform.sizeDelta.x;
			toolTipHeight = rectTransform.sizeDelta.y;
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
				canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1, Time.unscaledDeltaTime * 30);
			}
			else
			{
				if (disappearTimer > 0)
				{
					disappearTimer -= Time.unscaledDeltaTime;
					return;
				}

				canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0, Time.unscaledDeltaTime * 10);
			}
		}

		private Vector3 GetVec()
		{
			return new Vector3(
				Mathf.Clamp(Input.mousePosition.x, toolTipWidth / 2 + ToolTipPadding, Screen.width - toolTipWidth / 2 - ToolTipPadding),
				Mathf.Clamp(Input.mousePosition.y + 40, ToolTipPadding, Screen.height - toolTipHeight - ToolTipPadding), 0);
		}

		public void Hide()
		{
			// Debug.Log("Hide");
			isShow = false;
			disappearTimer = .3f;
		}
	}
}