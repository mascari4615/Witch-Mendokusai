using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerRotation : MonoBehaviour
	{
		[SerializeField] private bool useRotate;

		private const float ROTATE_SPEED = 150;
		private const float CAMERA_ROTATE_SPEED = 15;
		private float yRotation = 0;

		private void Update()
		{
			Rotate();
		}

		private void Rotate()
		{
			if (useRotate == false)
				return;

			if (Input.GetKey(KeyCode.Q))
				yRotation += Time.deltaTime * ROTATE_SPEED;
			if (Input.GetKey(KeyCode.E))
				yRotation -= Time.deltaTime * ROTATE_SPEED;

			Quaternion targetRotation = Quaternion.Euler(0, yRotation, 0);
			// transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5);
			transform.rotation = targetRotation;
			Camera.main.transform.parent.rotation = Quaternion.Lerp(Camera.main.transform.parent.rotation, targetRotation, Time.deltaTime * CAMERA_ROTATE_SPEED);
		}
	}
}