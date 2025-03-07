using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	public class ToolTipTrigger : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler
	{
		[field: SerializeField] public ToolTip ClickToolTip { get; private set; }
		[SerializeField] private bool usePopupToolTip = true;

		private SlotData slotData;

		private bool isPopupTooltipShowingThis = false;

		public void SetClickToolTip(ToolTip toolTip) => ClickToolTip = toolTip;

		public void SetToolTipContent(SlotData slotData)
		{
			this.slotData = slotData;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (usePopupToolTip == false)
				return;

			if (slotData == null || slotData.IsEmpty)
				return;

			ToolTipPopupManager.Instance.Show(slotData);
			isPopupTooltipShowingThis = true;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (usePopupToolTip == false)
				return;

			if (slotData == null || slotData.IsEmpty)
				return;

			ToolTipPopupManager.Instance.Hide();
			isPopupTooltipShowingThis = false;
		}

		private void OnDisable()
		{
			if (isPopupTooltipShowingThis)
				ToolTipPopupManager.Instance.Hide();
		}

		public void Trigger()
		{
			if (ClickToolTip == null)
				return;

			if (slotData == null || slotData.IsEmpty)
				return;

			ClickToolTip.SetToolTipContent(slotData);
		}
	}
}