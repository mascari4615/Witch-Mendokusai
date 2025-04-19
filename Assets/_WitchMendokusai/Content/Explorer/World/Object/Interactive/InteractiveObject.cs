using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class InteractiveObject : MonoBehaviour
	{
		public static readonly List<InteractiveObject> ActiveInteractives = new();

		public static InteractiveObject GetNearest(Vector3 targetPosition, float maxDistance)
		{
			return WMHelper.GetNearest(ActiveInteractives, element => element.transform.position, targetPosition, maxDistance);
		}

		private IInteractable[] interactable;

		private void Awake()
		{
			interactable = GetComponents<IInteractable>();
		}

		private void OnEnable()
		{
			ActiveInteractives.Add(this);
		}

		public void Interact()
		{
			foreach (IInteractable interact in interactable)
				interact.OnInteract();
		}

		private void OnDisable()
		{
			if (WMHelper.IsPlaying)
				ActiveInteractives.Remove(this);
		}
	}
}