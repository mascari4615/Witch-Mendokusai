using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public class Portal : MonoBehaviour
	{
		[field: SerializeField] public Transform TpPos { get; private set; }
		[field: SerializeField] public Stage TargetStage { get; private set; }
		[field: SerializeField] private int targetPortalIndex = -1;

		public void OnTriggerEnter(Collider other)
		{
			if (other.CompareTag("Player"))
			{
				UIManager.Instance.Transition.Transition(
					aDuringTransition: () =>
					{
						StageManager.Instance.LoadStage(TargetStage, targetPortalIndex);
					},
					aWhenEnd: () =>
					{
						UIManager.Instance.StagePopup(TargetStage);
					}).Forget();
			}
		}

		public void Active()
		{
			gameObject.layer = 0;
		}
	}
}