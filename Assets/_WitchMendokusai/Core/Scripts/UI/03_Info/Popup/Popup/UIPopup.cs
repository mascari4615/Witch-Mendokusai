using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIPopup : MonoBehaviour
	{
		[SerializeField] private UISlot slot;
		[SerializeField] private Animator animator;

		private readonly Queue<DataSO> dataSOs = new();
		private bool isPlaying = false;

		private void Start()
		{
			slot.Init();
		}

		public void Popup(DataSO dataSO)
		{
			dataSOs.Enqueue(dataSO);

			if (isPlaying)
				return;

			StartCoroutine(PopupCoroutine());
		}

		private IEnumerator PopupCoroutine()
		{
			isPlaying = true;

			WaitForSecondsRealtime ws01 = new(.1f);
			while (dataSOs.Count > 0)
			{
				DataSO targetDataSO = dataSOs.Dequeue();

				slot.SetSlot(targetDataSO);
				animator.SetTrigger("POP");

				yield return ws01;

				// 현재 재생되고 있는 애니메이션 클립의 이름이 "Idle"이 아닐 때까지 대기
				while (animator.GetCurrentAnimatorStateInfo(0).IsName("Popup_IDLE") == false)
					yield return null;
			}

			isPlaying = false;
		}
	}
}