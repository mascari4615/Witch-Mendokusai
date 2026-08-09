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
			// TASK-WM-217 — 세계는 하나다. 「멀티」 버튼이 씬에 남아 있어도 그냥 세계로 들어간다
			// (버튼 오브젝트 자체를 지우는 건 에디터 작업 — 그 전까지 눌러도 헷갈리지 않게 여기서 흡수).
			if (multiButton != null)
				multiButton.onClick.AddListener(StartGame);

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

			EnsureWorldLink().Enter();
			UISceneLoading.LoadScene("World");
		}

		/// <summary>
		/// 세계로 이어줄 문을 준비한다. 씬에 없으면 만든다 —
		/// 없다고 게임이 안 열리면 안 되기 때문이다(접속은 필요 조건이 아니다).
		/// </summary>
		private WorldLinkProvider EnsureWorldLink()
		{
			if (WorldLinkProvider.Instance != null)
				return WorldLinkProvider.Instance;

			GameObject holder = new GameObject(nameof(WorldLinkProvider));
			DontDestroyOnLoad(holder);
			return holder.AddComponent<WorldLinkProvider>();
		}

		/// <summary>
		/// ⚠ 폐기 예정 (TASK-WM-217) — 세계가 하나가 되면서 「방 만들기/참가」가 뜻을 잃었다.
		/// 남겨둔 이유는 하나뿐: 씬·프리팹이 아직 이걸 부를 수 있다. 부르면 그냥 세계로 들어간다.
		/// 씬에서 참조가 사라지면 이 메서드도 지운다.
		/// </summary>
		public void OpenMultiplayer()
		{
			Debug.Log($"{nameof(OpenMultiplayer)} — 세계는 하나다 (TASK-WM-217). 그냥 들어간다.");
			StartGame();
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