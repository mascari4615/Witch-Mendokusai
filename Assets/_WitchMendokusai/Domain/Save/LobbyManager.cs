using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class LobbyManager : MonoBehaviour
	{
		public static LobbyManager Instance { get; private set; }

		private const string LOBBY_SCENE = "Lobby";
		private const string WORLD_SCENE = "World";

		// 월드에서 로비로 이동 중 표시. 막이 덮이는 동안 다시 누름 무시, 다음 로비의 복귀 판정
		private static bool isReturningFromWorld;

		// 도메인 재로드를 끈 Play 에서 정적 값이 이전 Play 의 것으로 남지 않게
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => isReturningFromWorld = false;

		/// <summary> 월드에서만 로비로 갈 수 있음. 로비와 시작 화면에서는 버튼을 숨김 </summary>
		public static bool CanReturnFromWorld => SceneManager.GetActiveScene().name == WORLD_SCENE;

		/// <summary> 나가면 진행 중인 던전 판을 버리게 됨. 확인 창을 띄울지 정함 </summary>
		public static bool IsRunInProgress => DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) && dungeonManager.IsDungeon;

		/// <summary>
		/// 월드에서 로비로 (전체 메뉴와 설정창의 로비로). 던전 판은 정산 없이 버리고 마을 기준 상태로 되돌린 뒤 저장.
		/// 세계 연결도 끊고 (WorldDoor.Leave, 내 안의 세계 저장 포함) 막이 덮이는 동안 입력을 막음.
		/// 로비는 데이터를 다시 읽지 않음 (Start)
		/// </summary>
		public static void ReturnFromWorld(DataManager dataManager, InputManager inputManager)
		{
			if (CanReturnFromWorld == false)
				throw new InvalidOperationException($"{nameof(ReturnFromWorld)}: 월드 씬이 아님 ({SceneManager.GetActiveScene().name}). 로비로 버튼은 월드에서만 보여야 함");
			if (isReturningFromWorld)
				return;
			isReturningFromWorld = true;
			Debug.Log(nameof(ReturnFromWorld));

			inputManager.SetInputStrategy(new InputStrategyLoading());
			if (DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager))
				dungeonManager.AbandonRun();
			dataManager.SaveManager.SaveData();
			// 대화 시스템은 월드와 함께 사라지고 로비는 다시 읽지 않음. 다음 월드까지 기록을 들고 감
			DialogueHistoryBridge.HoldCurrent();
			WitchMendokusai.Net.WorldDoor.Leave();
			SceneFader.LoadScene(LOBBY_SCENE);
		}

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
			// 월드에서 돌아온 로비 (ReturnFromWorld) 는 데이터가 이미 메모리에 있음. 다시 읽으면 저장 전 상태로 덮임
			bool isReturn = isReturningFromWorld;
			isReturningFromWorld = false;
			if (isReturn)
				BootObserver.ReturnToLobby();
			BootObserver.Enter(BootPhase.Lobby); // TASK-WM-118 B1

			if (isReturn == false)
			{
				yield return StartCoroutine(dataManager.Init());
			}
			BootObserver.Enter(BootPhase.DataReady); // TASK-WM-118 B1
			Init();

			// 돌아온 로비에서 자동 시작하면 바로 월드로 되돌아감
			if (AppSetting.Data.AutoStart && isReturn == false)
			{
				StartGame();
			}
			else if (isReturn == false)
			{
				// 세이브를 앞 판으로 복구했거나 새 게임으로 시작했으면 처음 한 번 알림
				uiRoot.SaveNoticeView.ShowIfNeeded(dataManager.LastLoadOutcome);
			}
		}

		private void Init()
		{
			BuildMultiplayerButton();
			startButton.GetComponentInChildren<TextMeshProUGUI>().text = "시작하기";
			startButton.onClick.AddListener(StartGame);
			settingButton.onClick.AddListener(ToggleSettings);
			exitButton.onClick.AddListener(ExitGame);

			UpdateText();
		}

		// 시작 버튼 복제로 같은 모양 유지, 격자 배치라 씬 수정 없이 옆에 놓임
		private void BuildMultiplayerButton()
		{
			// 데모 릴리스 빌드는 멀티 미노출 (사용자 2026-11-12)
			if (Debug.isDebugBuild == false)
				return;
			Button multiplayerButton = Instantiate(startButton, startButton.transform.parent);
			multiplayerButton.name = "[Button] Multiplayer";
			multiplayerButton.transform.SetSiblingIndex(startButton.transform.GetSiblingIndex() + 1);
			multiplayerButton.GetComponentInChildren<TextMeshProUGUI>().text = "멀티";
			multiplayerButton.onClick.AddListener(StartMultiplayer);
		}

		private void UpdateText()
		{
			versionText.text = $"v{Application.version}";
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
			UISceneLoading.LoadScene(WORLD_SCENE);
		}

		/// <summary>멀티 시작. 멀리 있는 세계로 입장.</summary>
		public void StartMultiplayer()
		{
			Debug.Log(nameof(StartMultiplayer));

			WitchMendokusai.Net.WorldDoor.EnterShared();
			UISceneLoading.LoadScene(WORLD_SCENE);
		}

		public void ToggleSettings()
		{
			Debug.Log(nameof(ToggleSettings));
			// 세이브 알림이 떠 있으면 취소 키는 알림을 닫음
			if (uiRoot.SaveNoticeView.IsOpen)
			{
				uiRoot.SaveNoticeView.Cancel();
				return;
			}
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