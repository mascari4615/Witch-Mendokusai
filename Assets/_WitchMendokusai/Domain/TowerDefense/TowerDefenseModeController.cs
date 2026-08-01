using System.Collections;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 모드 컨트롤러 (TASK-WM-194 증분3) — ArenaModeController 미러. GameMode.TowerDefense
	/// 진입/이탈을 단일 ApplyMode 로 처리: 진입 = 모드 카메라 + 배치 입력 전략 + 매치 시작 + 배치 활성 /
	/// 이탈 = 매치 정리 + 배치 비활성 + 일반 카메라 + 월드 입력 전략. 모드 토글은 GameModeManager.SetMode
	/// (TowerDefense) 가 트리거(인게임 진입점 — 본 증분은 dev 메뉴 `WM/TowerDefense/Enter Mode`).
	///
	/// ★ 엣지 트리거 — ArenaModeController 와 동형: 전략 스왑·매치 Begin/Dispose 는 상태 전이(enter/exit)에서만
	///   1회. 초기 Start(Default) 재적용이 InputStrategySelector(씬 로드 시 World 전략 세팅)와 레이스/중복
	///   스왑하지 않도록 wasTowerDefense 로 전이만 감지.
	/// ★ 모드 카메라 = **정식 content 카메라**(ContentCameraMode.TowerDefense vcam, priority 전환).
	///   구 구현은 본편 카메라 *위에* 별도 Camera 를 덧대 렌더했는데, 그러면 밑에서 본편이 계속 돌고
	///   화면 기준 카메라가 둘로 갈라진다 — 게임 속 게임이라도 **진입한 순간 그 게임이 주체**여야 한다
	///   (사용자 지시). 월드→화면 변환이 숨은 본편 카메라를 잡아 데미지 숫자가 엉뚱한 데 뜨던 것이 그 증상.
	/// </summary>
	public class TowerDefenseModeController : MonoBehaviour
	{
		public static TowerDefenseModeController Instance { get; private set; }

		public static bool TryGetExistingInstance(out TowerDefenseModeController controller)
		{
			controller = Instance;
			return controller != null;
		}

		private GameModeManager gameModeManager;
		private InputManager inputManager;
		private UIRoot uiRoot;

		// HUD = 평범한 클래스(MonoBehaviour X) — [Inject] 메서드 타입당 1개 제약 회피. 최초 진입 시 lazy 생성.
		private TowerDefenseHudView hud;

		[SerializeField] private TowerDefenseMatch match;
		[SerializeField] private TowerDefensePlacement placement;
		[SerializeField] private TowerDefenseStageSO stage;
		[SerializeField] private Transform stageRoot;

		// 전이 감지 — 직전 적용이 TD 모드였는지. 초기 Default 재적용 no-op + enter/exit 1회 보장.
		private bool wasTowerDefense;


		[Inject]
		public void Construct(GameModeManager gameModeManager, InputManager inputManager, UIRoot uiRoot)
		{
			this.gameModeManager = gameModeManager;
			this.inputManager = inputManager;
			this.uiRoot = uiRoot;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void Start()
		{
			gameModeManager.OnModeChanged += OnGameModeChanged;
			match.MatchEnded += OnMatchEnded;
			ApplyMode(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;
			if (match != null)
				match.MatchEnded -= OnMatchEnded;
			if (Instance == this)
				Instance = null;
		}

		private void OnGameModeChanged(GameMode mode) => ApplyMode(mode);

		private void OnMatchEnded(TowerDefenseOutcome outcome)
		{
			// 무한 모드 = 버틴 웨이브가 곧 점수 → 기록을 남기지 않으면 판이 끝나도 아무것도 안 남는다.
			int best = match.WaveIndex;
			bool isNewRecord = false;
			if (DataManager.TryGetExistingInstance(out DataManager dataManager))
			{
				isNewRecord = TowerDefenseRecord.Submit(dataManager.TowerDefenseBestWave, stage.ID, match.WaveIndex, out best);
				if (isNewRecord)
					dataManager.SaveManager.SaveData();
			}

			Debug.Log($"{nameof(TowerDefenseModeController)}: 매치 종료 — outcome={outcome} wave={match.WaveIndex} best={best} newRecord={isNewRecord}");
			hud?.ShowOutcome(outcome, match.WaveIndex, best, isNewRecord);
		}

		/// <summary> 현재 스테이지 최고 기록 — 화면에 목표를 세워준다(없으면 0). </summary>
		private int CurrentBestRecord()
		{
			if (DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return 0;
			return TowerDefenseRecord.Best(dataManager.TowerDefenseBestWave, stage.ID);
		}

		// HUD 갱신 + 카메라 이동 — TD 모드 동안만.
		private void Update()
		{
			if (wasTowerDefense == false)
				return;

			hud?.Tick(match, stage);
		}

		/// <summary>
		/// 개척 시점을 무대에 맞춘다 — 진입 + 재시작 단일 경로.
		/// 카메라 자체는 <see cref="OverheadContentCameraController"/>(개척 vcam)가 구동하고,
		/// 여기서는 **무대가 아는 것**(개척지 중심·이동 한계·줌 범위)만 넘긴다. 수치는 스테이지 데이터가
		/// 정본이라 카메라 프리팹에 다시 박지 않는다.
		/// </summary>
		private void ResetCamera()
		{
			if (stage == null)
				return;
			if (OverheadContentCameraController.TryGet(ContentCameraMode.TowerDefense, out OverheadContentCameraController overhead) == false)
			{
				Debug.LogError($"{nameof(TowerDefenseModeController)}: 개척 vcam(ContentCameraMode.TowerDefense) 없음 — Camera 프리팹에 Camera_TowerDefense 확인 필요.");
				return;
			}

			Vector3 center = stageRoot != null ? stageRoot.position : Vector3.zero;
			overhead.SetFocusBounds(center, stage.CameraPanLimit);
			overhead.ConfigureZoom(stage.CameraMinHeight, stage.CameraMaxHeight, stage.CameraZoomSpeed);
			overhead.ResetView(center, yaw: 0f, height: stage.CameraInitialHeight);
		}

		/// <summary> 웨이브 진행 방식 전환(자동↔수동) — 진행 중인 매치에 즉시 반영된다. </summary>
		private void ToggleWaveMode()
		{
			placement.SuppressNextClick(); // 버튼 클릭이 배치로 새는 것 차단.
			match.AutoAdvanceWaves = match.AutoAdvanceWaves == false;
		}

		/// <summary> 다음 웨이브 호출 — 수동 진행의 진행 수단이자, 자동에서도 남은 건설 시간을 건너뛴다. </summary>
		private void CallNextWave()
		{
			placement.SuppressNextClick();
			match.RequestNextWave();
		}

		/// <summary>
		/// 처음부터 다시 — 매치를 통째로 버리고 새로 시작한다(사용자 지시: "재시작이 가능해야 할 듯.
		/// 오브젝트 풀 들어간 오브젝트들도 다시 잘 설정이 되어야 하고, 게임 값이나 설정도 마찬가지").
		///
		/// ★ 한 프레임 양보가 핵심: <see cref="TowerDefenseMatch.Dispose"/> 의 지면 Destroy 와 풀 반납은
		///   프레임 끝에 반영된다. 같은 프레임에 Begin 하면 옛 지면 콜라이더가 살아 있어 배치 레이캐스트가
		///   사라질 바닥을 맞고, 반납 중인 유닛을 그대로 되뽑아 원상복구가 무의미해진다.
		/// ★ 카메라·선택·배너까지 같이 되돌린다 — 하나라도 남으면 "새 판"이 아니다.
		/// </summary>
		public void Restart()
		{
			if (wasTowerDefense == false)
				return;

			// 버튼을 누른 그 클릭이 배치로도 새는 것을 **즉시** 삼킨다(코루틴 안에서 하면 같은 프레임에 늦는다).
			placement.SuppressNextClick();

			StopAllCoroutines();
			StartCoroutine(RestartRoutine());
		}

		private IEnumerator RestartRoutine()
		{
			match.Dispose();
			placement.Deactivate();

			yield return null;

			match.Begin(stage, stageRoot);
			placement.Activate();
			placement.SelectKind(TowerDefensePlaceableKind.Tower);
			ResetCamera();

			TowerDefenseHudView view = EnsureHud();
			if (view != null)
			{
				// Show 가 아니라 전용 리셋 — Show 는 본편 UI 를 다시 숨기며 복원 정보를 덮어쓴다(이미 숨긴 상태라 빈 목록이 됨).
				view.ResetForNewMatch(stage);
				view.SetBestRecord(CurrentBestRecord());
				view.SetSelectedKind(placement.SelectedKind);
			}

			Debug.Log($"{nameof(TowerDefenseModeController)}: 개척 재시작 — 새 매치 시작.");
		}

		private TowerDefenseHudView EnsureHud()
		{
			// init-order-ok: 모드 진입 시점 = World 부팅 완료 후라 uiRoot 준비 보장(lazy resolve).
			if (hud == null && uiRoot != null)
				hud = new TowerDefenseHudView(uiRoot);
			return hud;
		}

		private void ApplyMode(GameMode mode)
		{
			bool isTowerDefense = mode == GameMode.TowerDefense;

			if (isTowerDefense == wasTowerDefense)
				return; // 전이 아님 — 전략 스왑/매치 토글 생략(셀렉터 레이스·중복 방지).

			wasTowerDefense = isTowerDefense;

			if (isTowerDefense)
			{
				// 진입 — content 카메라 전환(개척 vcam 승격)은 CameraManager 단일 권위자가 GameMode 를 보고
				// 이미 처리한다. 여기서는 **무대가 아는 것**(시점 위치·경계·줌 범위)만 맞춘다.
				ResetCamera();
				inputManager.SetInputStrategy(new InputStrategyTowerDefense(placement, inputManager));
				match.Begin(stage, stageRoot);
				placement.Activate();
				TowerDefenseHudView view = EnsureHud();
				if (view != null)
				{
					view.Show(stage);
					view.SetBestRecord(CurrentBestRecord()); // 넘어야 할 선을 판 시작부터 보여준다.
					// 핫바 선택 표시 ↔ 실제 배치 대상은 같은 소스여야 한다(표시가 거짓말하면 오설치).
					view.SetSelectedKind(placement.SelectedKind);
					placement.SelectionChanged += view.SetSelectedKind;
					view.RestartRequested += Restart;
					view.WaveModeToggleRequested += ToggleWaveMode;
					view.NextWaveRequested += CallNextWave;
				}
			}
			else
			{
				// 이탈 — 매치 정리(멱등 Dispose) → 배치 비활성 → 모드 카메라 끄기 → 월드 입력 복귀.
				StopAllCoroutines(); // 재시작 코루틴이 이탈 뒤 재개해 매치를 되살리는 것 차단.
				match.Dispose();
				if (hud != null)
				{
					placement.SelectionChanged -= hud.SetSelectedKind;
					hud.RestartRequested -= Restart;
					hud.WaveModeToggleRequested -= ToggleWaveMode;
					hud.NextWaveRequested -= CallNextWave;
				}
				placement.Deactivate();
				hud?.Hide();
				inputManager.SetInputStrategy(new InputStrategyWorld());
			}
		}
	}
}
