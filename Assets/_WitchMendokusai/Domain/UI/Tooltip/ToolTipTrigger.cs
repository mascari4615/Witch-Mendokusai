using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace WitchMendokusai
{
	public class ToolTipTrigger : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler, ISelectHandler, IDeselectHandler
	{
		[field: SerializeField] public ToolTip ClickToolTip { get; private set; }
		[SerializeField] private bool usePopupToolTip = true;

		private SlotData slotData;

		private bool isPopupTooltipShowingThis = false;
		[SerializeField] private bool showOnSelect = false;
		private readonly Vector3[] corners = new Vector3[4];

		private ToolTipPopupManager toolTipPopupManager;

		[Inject]
		public void Construct(ToolTipPopupManager toolTipPopupManager)
		{
			this.toolTipPopupManager = toolTipPopupManager;
		}

		public void SetClickToolTip(ToolTip toolTip) => ClickToolTip = toolTip;

		public void SetToolTipContent(SlotData slotData)
		{
			this.slotData = slotData;
		}

		public void OnSelect(BaseEventData eventData)
		{
			if (showOnSelect == false || slotData == null || slotData.IsEmpty)
				return;

			((RectTransform)transform).GetWorldCorners(corners);
			toolTipPopupManager.ShowAt(slotData, (corners[1] + corners[2]) * 0.5f);
			isPopupTooltipShowingThis = true;
		}

		public void OnDeselect(BaseEventData eventData)
		{
			if (showOnSelect == false || isPopupTooltipShowingThis == false)
				return;

			toolTipPopupManager.Hide();
			isPopupTooltipShowingThis = false;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (usePopupToolTip == false)
				return;

			if (slotData == null || slotData.IsEmpty)
				return;

			toolTipPopupManager.Show(slotData);
			isPopupTooltipShowingThis = true;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (usePopupToolTip == false)
				return;

			if (slotData == null || slotData.IsEmpty)
				return;

			toolTipPopupManager.Hide();
			isPopupTooltipShowingThis = false;
		}

		private void OnDisable()
		{
			if (isPopupTooltipShowingThis)
				toolTipPopupManager.Hide();
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