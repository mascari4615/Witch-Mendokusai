using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class Rotate : MonoBehaviour
	{
		[SerializeField] private float speed = 100f;

		private void Update()
		{
			transform.Rotate(Vector3.forward, speed * Time.deltaTime);
		}
	}
}