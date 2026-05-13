using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace WitchMendokusai
{
	public class UINyang : MonoBehaviour
	{
		[SerializeField] private TextMeshProUGUI text;
		private int curValue;
		private Coroutine coroutine;

		private DataManager dataManager;

		[Inject]
		public void Construct(DataManager dataManager)
		{
			this.dataManager = dataManager;
		}

		private void Start()
		{
			text.text = "0냥";
			dataManager.GameStat.AddListener(GameStatType.NYANG, UpdateNyang);
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
			int targetValue = dataManager.GameStat[GameStatType.NYANG];
			while (curValue != targetValue)
			{
				curValue = (int)Mathf.Ceil(Mathf.SmoothStep(curValue, targetValue, .5f));
				if (curValue - targetValue < 3)
					curValue = targetValue;

				// text.text = curValue.ToString("N0") + "냥";
				text.text = curValue.ToString("N0");
				yield return null;
			}
		}
	}
}