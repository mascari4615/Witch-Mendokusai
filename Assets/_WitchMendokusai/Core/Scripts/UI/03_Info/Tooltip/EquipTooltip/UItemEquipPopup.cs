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
		private Coroutine lifeTimeLoop;
		private int curElementIndex = 0;

		private readonly WaitForSecondsRealtime popDelay = new(.1f);
		private readonly Queue<ItemData> toolTipStacks = new();

		private Color originColor = Color.clear;
		private float expireTime = LIFE_TIME;
		private const float LIFE_TIME = ANIM_TIME + WHITE_TIME + ANIM_TIME + WAIT_TIME;
		private const float WAIT_TIME = 1.5f;
		private const float ANIM_TIME = .2f;
		private const float WHITE_TIME = .3f;
		private bool IsLifeTime => expireTime > 0;

		// 한 번에 공개할 슬롯의 수
		private const int SLOT_COUNT = 8;
		private int showingSlotCount = 0;
		private int flag = 0;

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
				slots[i].Init();
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

				showingSlotCount++;
				if (showingSlotCount >= SLOT_COUNT)
					flag++;

				StartCoroutine(ShowToolTip(targetItemData, targetSlotIndex, flag));

				expireTime = LIFE_TIME;
				lifeTimeLoop ??= StartCoroutine(LifeTime());
			
				yield return popDelay;
			}

			showToolTipLoop = null;
		}

		private IEnumerator LifeTime()
		{
			while (IsLifeTime)
			{
				expireTime -= Time.unscaledDeltaTime;
				yield return null;
			}

			lifeTimeLoop = null;
		}

		private IEnumerator ShowToolTip(ItemData itemData, int slotIndex, int flag)
		{
			RuntimeManager.PlayOneShot($"event:/SFX/Equip");

			UIItemEquipPopupElement targetElement = elements[slotIndex];

			Image image = targetElement.image;
			CanvasGroup canvasGroup = targetElement.canvasGroup;
			UISlot targetSlot = targetElement.slot;

			targetSlot.SetSlot(itemData);
			targetSlot.transform.SetAsFirstSibling();
		
			{
				// 진입 애니메이션
				canvasGroup.DOFade(1, ANIM_TIME).SetUpdate(true);
				image.DOColor(Color.white, ANIM_TIME).SetUpdate(true);
				yield return targetSlot.transform.DOScale(Vector3.one * 1.2f, ANIM_TIME).SetUpdate(true).WaitForCompletion();
				yield return new WaitForSecondsRealtime(WHITE_TIME);
			
				// 대기
				targetSlot.transform.DOScale(Vector3.one, ANIM_TIME).SetUpdate(true);
				image.DOColor(originColor, ANIM_TIME).SetUpdate(true);
				yield return new WaitWhile(() => IsLifeTime && (flag == this.flag));
				showingSlotCount--;

				// 퇴장 애니메이션
				canvasGroup.DOFade(0, .5f).SetUpdate(true);
			}
		}

		public void StopToolTip()
		{
			StopAllCoroutines();
		}
	}
}