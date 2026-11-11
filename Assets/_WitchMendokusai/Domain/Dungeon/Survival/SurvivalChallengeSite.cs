using System;
using UnityEngine;

namespace WitchMendokusai
{
	public sealed class SurvivalChallengeSite : MonoBehaviour, IInteractable
	{
		private Func<Vector3, bool> tryStart;
		private bool consumed;
		public void Init(Func<Vector3, bool> tryStart)
		{
			this.tryStart = tryStart;
			consumed = false;
		}
		public void OnInteract()
		{
			if (consumed || tryStart == null) return;
			if (tryStart(transform.position) == false) return;
			consumed = true;
			gameObject.SetActive(false);
		}
	}
}
