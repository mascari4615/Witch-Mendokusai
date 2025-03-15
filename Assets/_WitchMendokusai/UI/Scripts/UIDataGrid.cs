using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public abstract class UIDataGrid<T> : MonoBehaviour, IUI
	{
		[field: SerializeField] public DataBufferSO<T> DataBufferSO { get; private set; }
		public List<T> Data { get; private set; } = new();
		public List<UISlot> Slots { get; protected set; } = new();
		public int CurSlotIndex { get; protected set; } = 0;
		
		[SerializeField] protected Transform slotsParent;
		[SerializeField] protected bool dontShowEmptySlot = false;
		[SerializeField] protected ToolTip clickToolTip;
		[SerializeField] protected GameObject noElementInfo;
		protected bool isInit = false;

		public UISlot CurSlot => Slots[CurSlotIndex];

		private void OnEnable()
		{
			UpdateUI();
		}

		/// <summary>
		/// UI 초기화, 한 번만 실행됨
		/// </summary>
		/// <returns></returns>
		public virtual bool Init()
		{
			if (isInit)
				return false;

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

			return isInit = true;
		}

		/// <summary>
		/// UI 갱신
		/// </summary>
		public virtual void UpdateUI()
		{
			if (!isInit)
				Init();

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

			if (clickToolTip != null)
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
		}
	}
}