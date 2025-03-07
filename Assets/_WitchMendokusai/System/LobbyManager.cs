using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	public class LobbyManager : MonoBehaviour
	{
		[SerializeField] private GameObject settingPanel;
		[SerializeField] private TextMeshProUGUI versionText;
		[SerializeField] private TextMeshProUGUI copyRightText;
		[SerializeField] private int year = 2024;

		private void Start()
		{
			Invoke(nameof(TryLogin), 1f);
			UpdateUI();
		}

		private void UpdateUI()
		{
			versionText.text = $"마녀여 영원히 v{Application.version}";
			copyRightText.text = $"© {year} {Application.companyName}";
		}

		private void TryLogin()
		{
			DataManager.Instance.PlayFabManager.Login();
		}

		public void StartGame()
		{
			Debug.Log(nameof(StartGame));
			UISceneLoading.LoadScene("World");
		}

		public void ExitGame()
		{
			Debug.Log(nameof(ExitGame));

#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
				settingPanel.SetActive(false);
		}
	}
}