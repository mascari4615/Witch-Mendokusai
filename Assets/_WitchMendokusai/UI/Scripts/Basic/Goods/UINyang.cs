using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public class UINyang : MonoBehaviour
	{
		[SerializeField] private TextMeshProUGUI text;
		private int curValue;
		private Coroutine coroutine;

		private void Start()
		{
			text.text = "0냥";
			SOManager.Instance.Nyang.OnValueChanged += UpdateNyang;
			UpdateNyang();
		}

		private void UpdateNyang()
		{
			if (coroutine != null)
				StopCoroutine(coroutine);
			coroutine = StartCoroutine(UpdateGoldCoroutine());
		}

		private IEnumerator UpdateGoldCoroutine()
		{
			int targetValue = SOManager.Instance.Nyang.RuntimeValue;
			while (curValue != targetValue)
			{
				curValue = (int)Mathf.Ceil(Mathf.SmoothStep(curValue, targetValue, .5f));
				if (curValue - targetValue < 3)
					curValue = targetValue;

				text.text = curValue.ToString("N0") + "냥";
				yield return null;
			}
		}
	}
}