using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIDollPanel : UIPanel
	{
		[SerializeField] private GameObject selectNewEquipmentPanel;
		private UIDollGrid dollGridUI;
		private UIItemGrid selectNewEquipmentInventoryUI;
		[SerializeField] private GameObject selectDollButton;
		[SerializeField] private List<UISlot> equipmentSlots;
		private int targetEquipmentIndex;

		// public void PrevDoll() => SetDoll(DataManager.Instance.CurGameData.curDollIndex - 1);
		// public void NextDoll() => SetDoll(DataManager.Instance.CurGameData.curDollIndex + 1);
		// private void SetDoll(int newDollIndex)
		// {
		// 	if (newDollIndex < 0)
		// 		newDollIndex = SOManager.Instance.Dolls.Length - 1;
		// 	else if (newDollIndex == SOManager.Instance.Dolls.Length)
		// 		newDollIndex = 0;

		// 	DataManager.Instance.CurGameData.curDollIndex = newDollIndex;
		// 	UpdateUI();
		// }

		public void SetDoll()
		{
			DataManager.Instance.SetCurDoll(dollGridUI.CurSlotIndex);
			Player.Instance.Object.SetDoll(DataManager.Instance.CurDollID);
			UpdateUI();
		}

		public override void Init()
		{
			dollGridUI = GetComponentInChildren<UIDollGrid>(true);
			dollGridUI.Init();
		
			selectNewEquipmentInventoryUI = selectNewEquipmentPanel.GetComponentInChildren<UIItemGrid>(true);
			selectNewEquipmentInventoryUI.Init();

			selectNewEquipmentPanel.SetActive(false);

			for (int i = 0; i < equipmentSlots.Count; i++)
			{
				equipmentSlots[i].SetSlotIndex(i);
				equipmentSlots[i].Init();

				if (i == 0) // 첫 번째 장비는 기본 장비로 고정됨
					continue;
				equipmentSlots[i].SetClickAction((slot) => { OpenChangeEquipmentPanel(slot.Index - 1); });
			}

			// 인형
			foreach (UISlot slot in dollGridUI.Slots)
			{
				slot.SetSelectAction((slot) =>
				{
					dollGridUI.SelectSlot(slot.Index);
					UpdateUI();
				});
			}

			for (int i = 0; i < selectNewEquipmentInventoryUI.Slots.Count; i++)
			{
				selectNewEquipmentInventoryUI.Slots[i].SetClickAction((slot) =>
				{
					selectNewEquipmentInventoryUI.SelectSlot(slot.Index);
					ChangeItem(slot.Index);
				});
			}
		}

		public override void UpdateUI()
		{
			int curDollID = dollGridUI.CurSlot.DataSO.ID;

			selectNewEquipmentPanel.SetActive(false);

			if (curDollID == Doll.DUMMY_ID)
			{
				equipmentSlots[0].transform.parent.gameObject.SetActive(false);
				selectDollButton.SetActive(false);
			}
			else
			{
				equipmentSlots[0].transform.parent.gameObject.SetActive(true);
				selectDollButton.SetActive(true);

				List<EquipmentData> equipments = DataManager.Instance.GetEquipmentData(curDollID);
				for (int i = 0; i < equipments.Count; i++)
					equipmentSlots[i].SetSlot(equipments[i]);
			}
		}

		private void OpenChangeEquipmentPanel(int equipmentIndex)
		{
			targetEquipmentIndex = equipmentIndex;
			selectNewEquipmentPanel.SetActive(true);
		}

		private void ChangeItem(int newItemSlotIndex)
		{
			// Debug.Log("ApplyNewDataSO" + slotIndex);
			Item newItem = SOManager.Instance.ItemInventory.GetItem(newItemSlotIndex);

			// UI에서 선택한 인형
			Doll curDoll = dollGridUI.CurSlot.DataSO as Doll;

			// 선택한 슬롯이 비어있는 경우
			if (newItem == null)
			{
				curDoll.EquipmentGuids[targetEquipmentIndex] = null;
				UpdateUI();
				return;
			}

			// 선택한 슬롯이 비어있지 않은 경우
			// 이 장비를 이미 이 인형이나 다른 인형이 착용하고 있는지 확인
			foreach (Doll doll in SOManager.Instance.DollBuffer.Data)
			{
				for (int ei = 0; ei < doll.EquipmentGuids.Count; ei++)
				{
					// 있다면 스왑
					if (doll.EquipmentGuids[ei] == newItem.Guid)
					{
						doll.EquipmentGuids[ei] = curDoll.EquipmentGuids[targetEquipmentIndex];
						curDoll.EquipmentGuids[targetEquipmentIndex] = newItem.Guid;
						UpdateUI();
						return;
					}
				}
			}

			// 누구도 착용하고 있지 않다면, 단순히 착용
			curDoll.EquipmentGuids[targetEquipmentIndex] = newItem.Guid;
			UpdateUI();
		}
	}
}