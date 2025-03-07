using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class PickupObject : MonoBehaviour, IInteractable
	{
		[SerializeField] private ItemData itemData;
		[SerializeField] private int amount;

		// Task
		// 오브젝트를 픽업하기 위한 선 작업(조건)
		// 예를 들어, 몇 초 동안 홀딩을 해야 픽업 가능하다던가, 특정 아이템을 가지고 있어야 한다던가 (도구류)

		[SerializeField] private List<CriteriaInfo> criteria;

		public void OnInteract()
		{
			SOManager.Instance.ItemInventory.Add(itemData, amount);
			gameObject.SetActive(false);
		}
	}
}