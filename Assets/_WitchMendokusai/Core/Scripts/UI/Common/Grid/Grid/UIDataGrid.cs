using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public abstract class UIDataGrid<T> : UIBase
	{
		public event Action<UISlot, T> OnSelectSlot = delegate { };

		[field: SerializeField] public DataBufferSO<T> DataBufferSO { get; private set; }
		public List<T> Data { get; private set; } = new();
		public List<UISlot> Slots { get; protected set; } = new();
		public int CurSlotIndex { get; protected set; } = 0;

		[SerializeField] protected Transform slotsParent;
		[SerializeField] protected bool dontShowEmptySlot = false;
		[SerializeField] protected ToolTip clickToolTip;
		[SerializeField] protected GameObject noElementInfo;

		public UISlot CurSlot => Slots.ElementAtOrDefault(CurSlotIndex);

		private void OnEnable()
		{
			UpdateUI();
		}

		public override void Init()
		{
			if (DataBufferSO != null)
				SetData(DataBufferSO.Data);

			if (slotsParent == null)
				slotsParent = transform;
			Slots = slotsParent.GetComponentsInChildren<UISlot>(true).ToList();

			for (int i = 0; i < Slots.Count; i++)
			{
				Slots[i].SetSlotIndex(i);
				Slots[i].SetSelectAction((slot) => { SelectSlot(slot.Index); });
				Slots[i].Init();

				if (clickToolTip != null)
					Slots[i].ToolTipTrigger.SetClickToolTip(clickToolTip);
			}

			SelectSlot(0);
		}

		public override void UpdateUI()
		{
			for (int i = 0; i < Slots.Count; i++)
			{
				if (DataBufferSO)
				{
					if (i < DataBufferSO.Data.Count)
					{
						Slots[i].SetSlot(DataBufferSO.Data[i] as DataSO);
						Slots[i].gameObject.SetActive(true);
					}
					else
					{
						Slots[i].SetSlot(null);

						if (dontShowEmptySlot)
							Slots[i].gameObject.SetActive(false);
					}
				}
				else
				{
					if (i < Data.Count)
					{
						Slots[i].SetSlot(Data[i] as DataSO);
						Slots[i].gameObject.SetActive(true);
					}
					else
					{
						Slots[i].SetSlot(null);

						if (dontShowEmptySlot)
							Slots[i].gameObject.SetActive(false);
					}
				}
			}

			if (clickToolTip != null && CurSlot != null && CurSlot.Data != null)
				clickToolTip.SetToolTipContent(CurSlot.Data);

			UpdateNoElementInfo();
		}

		protected void UpdateNoElementInfo()
		{
			if (clickToolTip != null)
			{
				if (Data.Count == 0)
					clickToolTip.Clear();
			}
			if (noElementInfo != null)
				noElementInfo.SetActive(Data.Count == 0);
		}

		public void SetDataBuffer(DataBufferSO<T> newDataBuffer)
		{
			DataBufferSO = newDataBuffer;
			SetData(DataBufferSO.Data);
		}

		public void SetData(List<T> newData)
		{
			Data = newData;
		}

		public void SelectSlot(int index)
		{
			CurSlotIndex = index;
			OnSelectSlot?.Invoke(CurSlot, Data.ElementAtOrDefault(index));
		}
	}
}