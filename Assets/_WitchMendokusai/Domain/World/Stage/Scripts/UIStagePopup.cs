using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIStagePopup : MonoBehaviour
	{
		[SerializeField] private UISlot slot;
		[SerializeField] private Animator animator;

		private void Awake()
		{
			slot.Init();
		}

		public void Popup(Stage stage)
		{
			slot.SetSlot(stage);
			animator.SetTrigger("POP");
		}
	}
}