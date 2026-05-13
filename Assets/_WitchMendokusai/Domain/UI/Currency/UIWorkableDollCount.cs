using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace WitchMendokusai
{
	public class UIWorkableDollCount : MonoBehaviour
	{
		[SerializeField] private TextMeshProUGUI text;
		private Coroutine coroutine;
		private WorkManager workManager;

		private DataManager dataManager;
		private SOManager soManager;

		[Inject]
		public void Construct(DataManager dataManager, SOManager soManager)
		{
			this.dataManager = dataManager;
			this.soManager = soManager;
			workManager = dataManager.WorkManager;
		}

		private void OnEnable()
		{
			coroutine = StartCoroutine(UpdateUI());
		}

		private void OnDisable()
		{
			StopCoroutine(coroutine);
		}

		private IEnumerator UpdateUI()
		{
			WaitForSeconds wait = new(.1f);
			while (true)
			{
				yield return wait;

				if (dataManager.IsDataLoaded == false)
					continue;

				int workableDollCount = soManager.DollBuffer.Data.Count;
				workableDollCount -= workManager.GetWorkCount(WorkListType.DollWork) + workManager.GetWorkCount(WorkListType.DummyWork);
				// text.text = $"{workableDollCount}/{soManager.DollBuffer.Data.Count} 인형";
				text.text = $"{workableDollCount}/{soManager.DollBuffer.Data.Count}";
			}
		}
	}
}