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
		public void Construct(DataManager dataManager, UIRoot uiRoot, InputStrategySelector inputStrategySelector)
		{
			this.dataManager = dataManager;
			this.uiRoot = uiRoot;
			// Lobby 씬은 스코프가 없어 선택기가 이 매니저를 못 받음. 여기서 넘김
			inputStrategySelector.BindLobby(this);
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
			BuildMultiplayerButton();
			startButton.GetComponentInChildren<TextMeshProUGUI>().text = "싱글";
			startButton.onClick.AddListener(StartGame);
			settingButton.onClick.AddListener(ToggleSettings);
			exitButton.onClick.AddListener(ExitGame);

			UpdateText();
		}

		// 시작 버튼 복제로 같은 모양 유지, 격자 배치라 씬 수정 없이 옆에 놓임
		private void BuildMultiplayerButton()
		{
			Button multiplayerButton = Instantiate(startButton, startButton.transform.parent);
			multiplayerButton.name = "[Button] Multiplayer";
			multiplayerButton.transform.SetSiblingIndex(startButton.transform.GetSiblingIndex() + 1);
			multiplayerButton.GetComponentInChildren<TextMeshProUGUI>().text = "멀티";
			multiplayerButton.onClick.AddListener(StartMultiplayer);
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
		/// <summary>싱글 시작. 서버 접속 없이 내 안의 세계로 입장.</summary>
		public void StartGame()
		{
			Debug.Log(nameof(StartGame));

			// 문은 통신 층이 스스로 세우고 여기 꽂아 둔다 (WorldDoor) — 로비는 통신을 몰라야 한다.
			// 어셈블리 방향이 Network → Domain 단방향이라, 로비가 문의 타입을 직접 부르면 컴파일이 깨진다.
			WitchMendokusai.Net.WorldDoor.EnterSolo();
			UISceneLoading.LoadScene("World");
		}

		/// <summary>멀티 시작. 멀리 있는 세계로 입장.</summary>
		public void StartMultiplayer()
		{
			Debug.Log(nameof(StartMultiplayer));

			WitchMendokusai.Net.WorldDoor.EnterShared();
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