using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using DG.Tweening;

namespace WitchMendokusai
{
	public class UItemEquipPopup : MonoBehaviour
	{
		private UISlot[] slots;
		private CanvasGroup[] slotCanvasGroups;

		private Coroutine showToolTipLoop;
		private int curElementIndex = 0;

		private readonly WaitForSecondsRealtime ws01 = new(.1f);
		private readonly Queue<ItemData> toolTipStacks = new();
	
		private void Awake()
		{
			slots = GetComponentsInChildren<UISlot>(true);
			slotCanvasGroups = GetComponentsInChildren<CanvasGroup>(true);
		}

		private void Start()
		{
			SOManager.Instance.LastEquipedItem.OnValueChanged += EquipItem;
		}
		
		public void EquipItem()
		{
			toolTipStacks.Enqueue(SOManager.Instance.LastEquipedItem.RuntimeValue);

			if (showToolTipLoop == null)
				showToolTipLoop = StartCoroutine(ShowToolTips());
		}

		private IEnumerator ShowToolTips()
		{
			while (toolTipStacks.Count > 0)
			{
				ItemData targetItemData = toolTipStacks.Dequeue();

				int targetSlotIndex = curElementIndex;
				curElementIndex = (curElementIndex + 1) % slots.Length;

				ShowToolTip(targetItemData, targetSlotIndex);
				yield return ws01;
			}

			showToolTipLoop = null;
		}

		private void ShowToolTip(ItemData itemData, int slotIndex)
		{
			RuntimeManager.PlayOneShot($"event:/SFX/Equip");
		
			UISlot targetSlot = slots[slotIndex];
			targetSlot.SetSlot(itemData);
			targetSlot.transform.SetAsFirstSibling();

			CanvasGroup targetCanvasGroup = slotCanvasGroups[slotIndex];
			targetCanvasGroup.DOFade(1, .2f).OnComplete(() => targetCanvasGroup.DOFade(0, .5f).SetDelay(1f));
		}

		public void StopToolTip()
		{
			StopAllCoroutines();
		}
	}
}