using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class LobbyManager : MonoBehaviour
	{
		public static LobbyManager Instance { get; private set; }

		[SerializeField] private TextMeshProUGUI versionText;
		[SerializeField] private TextMeshProUGUI copyRightText;
		[SerializeField] private int year;

		[SerializeField] private UISetting setting;
		[SerializeField] private Button startButton, settingButton, exitButton;

		private IEnumerator Start()
		{
			Debug.Log($"{nameof(LobbyManager)} {nameof(Start)}");
			Debug.Log($"Application.version: {Application.version}");

			Instance = this;

			yield return StartCoroutine(DataManager.Instance.Init());
			DataManager.Instance.Login();
			Init();

			if (AppSetting.Data.AutoStart)
			{
				StartGame();
			}
		}

		private void Init()
		{
			// Bind Event
			{
				startButton.onClick.AddListener(StartGame);
				settingButton.onClick.AddListener(ToggleSettings);
				exitButton.onClick.AddListener(ExitGame);
			}

			// Init
			{
				setting.Init(null);
				UpdateText();
			}
		}

		private void UpdateText()
		{
			versionText.text = $"마녀여 영원히 v{Application.version}";
			copyRightText.text = $"© {year} {Application.companyName}";
		}

		private void OnDisable()
		{
			Instance = null;
		}

		#region Button
		public void StartGame()
		{
			Debug.Log(nameof(StartGame));
			UISceneLoading.LoadScene("World");
		}

		public void ToggleSettings()
		{
			Debug.Log(nameof(ToggleSettings));
			setting.SetActive(!setting.gameObject.activeSelf);
			setting.UpdateUI();
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
		#endregion
	}
}