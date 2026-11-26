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
		private GameStat subscribedStat;

		[Inject]
		public void Construct(DataManager dataManager)
		{
			this.dataManager = dataManager;
		}

		private void Start()
		{
			text.text = "0냥";
			subscribedStat = dataManager.GameStat;
			subscribedStat.AddListener(GameStatType.NYANG, UpdateNyang);
			UpdateNyang();
		}

		// 통계는 씬보다 오래 삶. 안 풀면 사라진 화면이 냥 변경을 계속 들음
		private void OnDestroy() => subscribedStat?.RemoveListener(GameStatType.NYANG, UpdateNyang);

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
				text.text = curValue.ToString("N0") + "냥";
				yield return null;
			}
		}
	}
}