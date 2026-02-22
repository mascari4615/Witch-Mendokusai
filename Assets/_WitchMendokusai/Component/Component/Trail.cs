using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class Trail : MonoBehaviour
	{
		[SerializeField] private TrailRenderer trailRenderer;

		private void OnEnable()
		{
			Clear();
		}

		public void Clear()
		{
			trailRenderer.Clear();
		}
	}
}