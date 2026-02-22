
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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

		public override void Init()
		{
			base.Init();

			priceText = transform.Find("[Text] Price").GetComponent<TextMeshProUGUI>();
		}

		public override void UpdateUI()
		{
			base.UpdateUI();

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

		public void SetUIItemGrid(UIItemGrid itemGridUI) => UIItemGrid = itemGridUI;
		public void SetPriceType(PriceType priceType)
		{
			this.priceType = priceType;
			UpdateUI();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (canPlayerSetItem == false)
				return;

			if (canHold == false)
				return;

			UIHoldingSlot.Instance.DoSomething(this, eventData.button == PointerEventData.InputButton.Left);
		}
	}
}