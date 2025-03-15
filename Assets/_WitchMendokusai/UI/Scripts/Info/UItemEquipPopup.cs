using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using DG.Tweening;
using System.Linq;

namespace WitchMendokusai
{
	public class UItemEquipPopup : MonoBehaviour
	{
		private class UIItemEquipPopupElement
		{
			public UISlot slot;
			public CanvasGroup canvasGroup;
			public Image image;
		}

		private UIItemEquipPopupElement[] elements;

		private Coroutine showToolTipLoop;
		private int curElementIndex = 0;

		private readonly WaitForSecondsRealtime popDelay = new(.2f);
		private readonly Queue<ItemData> toolTipStacks = new();

		private Color originColor = Color.clear;

		private void Awake()
		{
			Init();
		}

		private void Init()
		{
			UISlot[] slots = GetComponentsInChildren<UISlot>(true);

			elements = new UIItemEquipPopupElement[slots.Length];
			for (int i = 0; i < slots.Length; i++)
			{
				elements[i] = new UIItemEquipPopupElement
				{
					slot = slots[i],
					canvasGroup = slots[i].GetComponent<CanvasGroup>(),
					image = slots[i].GetComponent<Image>()
				};
			}

			originColor = elements.First().image.color;
		}

		private void Start()
		{
			SOManager.Instance.LastEquippedItem.OnValueChanged += EquipItem;
		}

		public void EquipItem()
		{
			toolTipStacks.Enqueue(SOManager.Instance.LastEquippedItem.RuntimeValue);
			showToolTipLoop ??= StartCoroutine(ShowToolTips());
		}

		private IEnumerator ShowToolTips()
		{
			while (toolTipStacks.Count > 0)
			{
				ItemData targetItemData = toolTipStacks.Dequeue();

				int targetSlotIndex = curElementIndex;
				curElementIndex = (curElementIndex + 1) % elements.Length;

				ShowToolTip(targetItemData, targetSlotIndex);
				yield return popDelay;
			}

			showToolTipLoop = null;
		}

		private void ShowToolTip(ItemData itemData, int slotIndex)
		{
			RuntimeManager.PlayOneShot($"event:/SFX/Equip");

			UIItemEquipPopupElement targetElement = elements[slotIndex];
			float whiteTime = .3f;
			float lifeTime = 2f;
			{
				UISlot targetSlot = targetElement.slot;
				targetSlot.SetSlot(itemData);
				targetSlot.transform.SetAsFirstSibling();

				targetSlot.transform.DOScale(Vector3.one * 1.2f, .2f).OnComplete(() => targetSlot.transform.DOScale(Vector3.one, .2f).SetDelay(whiteTime));

				CanvasGroup targetCanvasGroup = targetElement.canvasGroup;
				targetCanvasGroup.DOFade(1, .2f).OnComplete(() => targetCanvasGroup.DOFade(0, .5f).SetDelay(lifeTime));

				Image targetImage = targetElement.image;
				targetImage.color = Color.white;
				targetImage.DOColor(originColor, .2f).SetDelay(whiteTime);
			}
		}

		public void StopToolTip()
		{
			StopAllCoroutines();
		}
	}
}