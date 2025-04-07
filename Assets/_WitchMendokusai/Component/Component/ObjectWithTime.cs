using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectWithTime : MonoBehaviour
{
	[SerializeField] private float duration = 1f;
	private bool isFirstInvoke = true;
	private Coroutine coroutine;

	private void OnEnable()
	{
		if (isFirstInvoke)
		{
			isFirstInvoke = false;
			return;
		}

		coroutine = StartCoroutine(DisableObject());

		// duration 보다 일찍 꺼지는 경우가 있어서
		// Invoke(nameof(DisableObject), duration);
	}

	private IEnumerator DisableObject()
	{
		yield return new WaitForSeconds(duration);
		gameObject.SetActive(false);
	}

	private void OnDisable()
	{
		if (coroutine != null)
			StopCoroutine(coroutine);
	}
}
