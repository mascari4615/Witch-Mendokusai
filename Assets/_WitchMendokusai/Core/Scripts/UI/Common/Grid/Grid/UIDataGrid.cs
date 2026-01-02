using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;
using System;

namespace WitchMendokusai
{
	public abstract class UIDataGrid<T> : UIBase
	{
		[field: SerializeField] public DataBufferSO<T> DataBufferSO { get; private set; }
		public List<T> Data { get; private set; } = new();
		public List<UISlot> Slots { get; protected set; } = new();
		public int CurSlotIndex { get; protected set; } = 0;
		private Func<T, bool> filterFunc = null;

		[SerializeField] protected Transform slotsParent;
		[SerializeField] protected bool showEmptySlot = true;
		[SerializeField] protected ToolTip clickToolTip;
		[SerializeField] protected GameObject noElementInfo;

		public UISlot CurSlot => Slots[CurSlotIndex];

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
			// TODO: CurSlotIndex 보정

			for (int i = 0; i < Slots.Count; i++)
			{
				T data = Data.ElementAtOrDefault(i);
				bool canDisplaySlot = data != null && (filterFunc == null || filterFunc(data));

				Slots[i].gameObject.SetActive(canDisplaySlot || showEmptySlot);
				SetSlotData(i, data);
			}

			if (clickToolTip != null)
				clickToolTip.SetToolTipContent(CurSlot.Data);

			UpdateNoElementInfo();
		}

		protected virtual void SetSlotData(int index, T data)
		{
			if (data == null)
			{
				Slots[index].SetSlot(null);
				return;
			}

			Slots[index].SetSlot(data as DataSO);
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
			Data = newDataBuffer.Data; // 참조 복사
		}

		/// <summary> DataBuffer를 사용하는 경우 대신 SetDataBuffer 사용 </summary>
		public void SetData(List<T> newData)
		{
			Data = newData;
		}

		public void SelectSlot(int index)
		{
			CurSlotIndex = index;
		}

		public void SetFilterFunc(Func<T, bool> newFilterFunc) => filterFunc = newFilterFunc;
		public void ClearFilterFunc() => SetFilterFunc(null);
	}
}