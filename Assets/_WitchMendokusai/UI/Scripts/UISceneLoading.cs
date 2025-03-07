using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// https://wergia.tistory.com/220
	public class UISceneLoading : MonoBehaviour
	{
		[SerializeField] private Image image;
		[SerializeField] private TextMeshProUGUI text;

		private static string sceneName;

		public static void LoadScene(string targetSceneName)
		{
			sceneName = targetSceneName;
			SceneManager.LoadScene("Loading");
		}

		private void Start()
		{
			StartCoroutine(LoadSceneAsync());
		}

		private IEnumerator LoadSceneAsync()
		{
			const float minTime = 1;
			float time = 0;

			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
			operation.allowSceneActivation = false;

			while ((operation.progress < .9f) || (minTime >= time))
			{
				image.fillAmount = operation.progress / .9f;
				text.text = $"Loading... {image.fillAmount * 100}%";
				time += Time.deltaTime;
				yield return null;
			}

			text.text = $"로딩 완료 !";

			yield return new WaitForSeconds(.5f);

			operation.allowSceneActivation = true;
		}
	}
}