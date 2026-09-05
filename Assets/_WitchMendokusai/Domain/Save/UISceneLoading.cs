using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// TODO: SceneManager
	// https://wergia.tistory.com/220
	public class UISceneLoading : MonoBehaviour
	{
		// Unity 는 allowSceneActivation=false 일 때 진행률을 0.9 에서 멈춘다. 엔진이 정한 값이라
		// 조절 대상이 아니지만, 두 곳에 0.9f 로 박혀 있으면 무슨 뜻인지 안 보인다.
		private const float SCENE_READY_PROGRESS = 0.9f;

		[SerializeField] private Image image;
		[SerializeField] private TextMeshProUGUI text;

		[Header("Loading Feel")]
		// 최소 로딩 시간 — 너무 빨리 지나가면 화면이 깜빡인 것처럼 보인다.
		[SerializeField] private float minLoadingTime = 1f;
		// 「로딩 완료」를 보여주고 넘어가기까지.
		[SerializeField] private float completedHoldTime = 0.5f;

		private static string sceneName;

		public static void LoadScene(string targetSceneName)
		{
			sceneName = targetSceneName;
			BootObserver.Enter(BootPhase.SceneLoading); // TASK-WM-118 B1
			SceneManager.LoadScene("Loading");
		}

		private void Start()
		{
			StartCoroutine(LoadSceneAsync());
		}

		private IEnumerator LoadSceneAsync()
		{
			float time = 0;

			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
			operation.allowSceneActivation = false;

			while ((operation.progress < SCENE_READY_PROGRESS) || (minLoadingTime >= time))
			{
				image.fillAmount = operation.progress / SCENE_READY_PROGRESS;
				text.text = $"Loading... {image.fillAmount * 100}%";
				time += Time.deltaTime;
				yield return null;
			}

			text.text = $"로딩 완료 !";

			yield return new WaitForSeconds(completedHoldTime);

			operation.allowSceneActivation = true;
		}
	}
}