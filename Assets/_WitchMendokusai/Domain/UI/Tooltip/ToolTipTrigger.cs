using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class ToolTipTrigger : MonoBehaviour, IPointerExitHandler, IPointerEnterHandler
	{
		[field: SerializeField] public ToolTip ClickToolTip { get; private set; }
		[SerializeField] private bool usePopupToolTip = true;

		private SlotData slotData;

		private bool isPopupTooltipShowingThis = false;

		private ToolTipPopupManager toolTipPopupManager;

		[Inject]
		public void Construct(ToolTipPopupManager toolTipPopupManager)
		{
			this.toolTipPopupManager = toolTipPopupManager;
		}

		private void Awake()
		{
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
		}

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