
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public enum PriceType
	{
		Buy,
		Sell
	}

	public class UIItemSlot : UISlot, IPointerDownHandler
	{
		protected TextMeshProUGUI priceText;

		public UIItemGrid UIItemGrid { get; private set; }
		public Inventory Inventory => UIItemGrid.DataBufferSO as Inventory;

		public bool canPlayerSetItem = true;
		public bool canHold = true;
		private PriceType priceType = PriceType.Buy;

		private UIHoldingSlot uiHoldingSlot;

		[Inject]
		public void Construct(UIHoldingSlot uiHoldingSlot)
		{
			this.uiHoldingSlot = uiHoldingSlot;
		}

		protected virtual void Awake()
		{
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
		}

		public override void Init()
		{
			base.Init();

			priceText = transform.Find("[Text] Price")?.GetComponent<TextMeshProUGUI>();
		}

		public override void UpdateUI()
		{
			base.UpdateUI();

			if (priceText != null)
			{
				if (DataSO)
				{
					ItemData itemData = DataSO as ItemData;
					priceText.text = (priceType == PriceType.Buy) ? itemData.PurchasePrice.ToString() : itemData.SalePrice.ToString();
				}
				else
				{
					priceText.text = string.Empty;
				}
			}
		}

		public void SetUIItemGrid(UIItemGrid itemGridUI) => UIItemGrid = itemGridUI;
		public void SetPriceType(PriceType priceType)
		{
			this.priceType = priceType;
			UpdateUI();
		}

		public virtual void OnPointerDown(PointerEventData eventData)
		{
			if (canPlayerSetItem == false)
				return;

			if (canHold == false)
				return;

			uiHoldingSlot.DoSomething(this, eventData.button == PointerEventData.InputButton.Left);
		}
	}
}