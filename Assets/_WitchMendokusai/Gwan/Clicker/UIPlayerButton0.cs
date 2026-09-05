using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIPlayerButton0 : MonoBehaviour
	{
		private static readonly int State = Animator.StringToHash("STATE");
		private static readonly int Update = Animator.StringToHash("UPDATE");

		[SerializeField] private Animator animator;

		public void UpdateIcon()
		{
			// animator.SetInteger(State, SOManager.Instance.CanInteract.RuntimeValue ? 1 : 0);
			animator.SetTrigger(Update);
		}

		public void Down()
		{
			// isPlayerButton0Down.RuntimeValue = true;
		}

		public void Up()
		{
			// isPlayerButton0Down.RuntimeValue = false;
		}
	}
}