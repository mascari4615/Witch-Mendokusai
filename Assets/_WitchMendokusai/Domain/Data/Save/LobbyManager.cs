using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class LobbyManager : MonoBehaviour
	{
		public static LobbyManager Instance { get; private set; }

		[SerializeField] private TextMeshProUGUI versionText;
		[SerializeField] private TextMeshProUGUI copyRightText;
		[SerializeField] private int year;

		[SerializeField] private Button startButton, settingButton, exitButton;
		[SerializeField] private Button multiButton; // TASK-WM-191 「멀티」 — 멀티 로비 패널 Open

		private DataManager dataManager;
		private UIRoot uiRoot;

		[Inject]
		public void Construct(DataManager dataManager, UIRoot uiRoot)
		{
			this.dataManager = dataManager;
			this.uiRoot = uiRoot;
		}

		private void Awake()
		{
			LifetimeScope scope = LifetimeScope.Find<SceneLifetimeScope>();
			if (scope == null)
				scope = LifetimeScope.Find<RootLifetimeScope>();
			scope?.Container.Inject(this);
		}

		private IEnumerator Start()
		{
			Debug.Log($"{nameof(LobbyManager)} {nameof(Start)}");
			Debug.Log($"Application.version: {Application.version}");

			Instance = this;
			BootObserver.Enter(BootPhase.Lobby); // TASK-WM-118 B1

			yield return StartCoroutine(dataManager.Init());
			dataManager.Login();
			BootObserver.Enter(BootPhase.DataReady); // TASK-WM-118 B1
			Init();

			if (AppSetting.Data.AutoStart)
			{
				StartGame();
			}
		}

		private void Init()
		{
			startButton.onClick.AddListener(StartGame);
			settingButton.onClick.AddListener(ToggleSettings);
			exitButton.onClick.AddListener(ExitGame);
			// 「멀티」 — 멀티 로비 패널(호스트/참가) Open. 옵션 진입: 미할당 시 가드(타이틀 코어 무영향).
			if (multiButton != null)
				multiButton.onClick.AddListener(OpenMultiplayer);

			UpdateText();
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

		public void OpenMultiplayer()
		{
			Debug.Log(nameof(OpenMultiplayer));
			if (MultiplayerLobbyController.Instance == null)
			{
				Debug.LogWarning("[Lobby] MultiplayerLobbyController 미등록 — 멀티 패널 못 엶");
				return;
			}
			MultiplayerLobbyController.Instance.Open();
		}

		public void ToggleSettings()
		{
			Debug.Log(nameof(ToggleSettings));
			uiRoot.SettingView.Toggle();
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