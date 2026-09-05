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
		/// <summary>
		/// 세계로 들어간다 (TASK-WM-217). 「혼자/같이」를 묻지 않는다 —
		/// 멀리 있는 세계에 붙으면 거기로, 못 붙으면 내 안의 세계로 들어간다.
		/// </summary>
		public void StartGame()
		{
			Debug.Log(nameof(StartGame));

			// 문은 통신 층이 스스로 세우고 여기 꽂아 둔다 (WorldDoor) — 로비는 통신을 몰라야 한다.
			// 어셈블리 방향이 Network → Domain 단방향이라, 로비가 문의 타입을 직접 부르면 컴파일이 깨진다.
			WitchMendokusai.Net.WorldDoor.Enter();
			UISceneLoading.LoadScene("World");
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