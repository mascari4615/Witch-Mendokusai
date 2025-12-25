using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class IntroManager : MonoBehaviour
	{
		[SerializeField] private GameObject[] panels;

		private IEnumerator Start()
		{
			Debug.Log($"{nameof(IntroManager)} {nameof(Start)}");

			InitView();

			if (AppSetting.Data.UseIntro)
			{
				yield return StartCoroutine(Intro());
			}

			LoadLobby();
		}

		private void InitView()
		{
			for (int i = 0; i < panels.Length; i++)
			{
				panels[i].SetActive(false);
			}
		}

		private IEnumerator Intro()
		{
			yield return new WaitForSeconds(1f);

			for (int i = 0; i < panels.Length; i++)
			{
				panels[i].SetActive(true);
				yield return new WaitForSeconds(3f); // 각 패널을 3초 동안 표시
				panels[i].SetActive(false);
			}

			yield return new WaitForSeconds(1f); // 마지막 패널 후 잠시 대기
		}
		
		private void LoadLobby()
		{
			SceneManager.LoadScene("Lobby");
		}
	}
}