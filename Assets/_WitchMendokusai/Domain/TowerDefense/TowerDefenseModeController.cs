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
	/// ★ 모드 카메라 = 평범한 high-depth Camera(ArenaModeController.spectatorCamera 와 동일 근거) — MCamera/
	///   CameraManager 의 ContentCameraMode 리그는 *플레이어 추종 궤도*라 고정 개척뷰가 안 맞음(WM-165 실증,
	///   ArenaModeController 헤더 참조). CameraManager.SetContentCameraMode 호출 X.
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
		[Tooltip("개척 모드 카메라 — 진입 시 활성(depth 높아 플레이어 카메라 위 풀스크린 렌더). 평범한 Camera, ContentCameraMode 리그 아님.")]
		[SerializeField] private Camera modeCamera;

		// 전이 감지 — 직전 적용이 TD 모드였는지. 초기 Default 재적용 no-op + enter/exit 1회 보장.
		private bool wasTowerDefense;

		// 카메라 상태(포커스/회전/높이) — 도시 부감 카메라와 같은 수학을 쓰는 공용 리그.
		private readonly OverheadCameraRig cameraRig = new();

		// Ctrl 가속 배수 — 도시 부감 카메라(boostMultiplier 기본값)와 같은 감각.
		private const float CAMERA_BOOST_MULTIPLIER = 3f;

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
			Debug.Log($"{nameof(TowerDefenseModeController)}: 매치 종료 — outcome={outcome}");
			hud?.ShowOutcome(outcome, match.WaveIndex);
		}

		// HUD 갱신 + 카메라 이동 — TD 모드 동안만.
		private void Update()
		{
			if (wasTowerDefense == false)
				return;

			hud?.Tick(match, stage);
			UpdateCamera();
		}

		/// <summary>
		/// 개척 카메라 조작 — 이동(WASD) · 회전(Q/E) · **휠 줌**.
		/// 거동 수학은 <see cref="OverheadCameraRig"/> 단일 정본에 있다(도시 부감 카메라와 같은 것).
		/// 축도 게임 전체 공용(`CameraMove`/`CameraRotate`/`ScrollWheel`)이라 조작감이 통일되고,
		/// 플레이어 Move 축과 분리돼 있어 이 모드에서 캐릭터가 딸려 움직일 위험이 없다.
		/// </summary>
		private void UpdateCamera()
		{
			if (modeCamera == null || stage == null || inputManager == null)
				return;

			OverheadCameraRig.DriveInput input = new()
			{
				Move = inputManager.CameraMoveInput,
				// 좌우 회전은 아직 0 — 개척 입력 전략이 CameraRotate 축을 막고 있고(플레이어 캐릭터가 없는 모드),
				// 그 축을 열면 화면 뒤에 있는 본편 카메라까지 같이 돌아 나갈 때 시점이 바뀐 채로 남는다.
				// 회전을 붙일 땐 개척 전용 축을 새로 내는 게 맞다(리그는 이미 받을 준비가 돼 있음).
				Rotate = 0f,
				ScrollDelta = inputManager.ScrollWheelDelta,
				SpeedMultiplier = inputManager.IsCameraBoost ? CAMERA_BOOST_MULTIPLIER : 1f,
			};

			cameraRig.Drive(input, RigSettings, Time.deltaTime, modeCamera.transform);
		}

		// 무대가 유한하므로 포커스를 스테이지 중심 기준으로 가둔다(개척지를 화면 밖으로 잃어버리지 않게).
		private OverheadCameraRig.Settings RigSettings => new()
		{
			PanSpeed = stage.CameraPanSpeed,
			YawSpeed = stage.CameraYawSpeed,
			FixedPitch = stage.CameraPitch,
			MinHeight = stage.CameraMinHeight,
			MaxHeight = stage.CameraMaxHeight,
			ZoomSpeed = stage.CameraZoomSpeed,
			ClampFocus = true,
			FocusCenter = stageRoot != null ? stageRoot.position : Vector3.zero,
			FocusLimit = stage.CameraPanLimit,
		};

		/// <summary> 시점을 시작 상태로 — 진입 + 재시작 단일 경로(재시작인데 시점만 남으면 리셋이 거짓말). </summary>
		private void ResetCamera()
		{
			if (modeCamera == null || stage == null)
				return;

			cameraRig.Reset(
				stageRoot != null ? stageRoot.position : Vector3.zero,
				yaw: 0f,
				height: Mathf.Clamp(stage.CameraInitialHeight, stage.CameraMinHeight, stage.CameraMaxHeight));
			cameraRig.Apply(RigSettings, modeCamera.transform);
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
				// 진입 — 모드 카메라 켜기 → 배치 입력 전략 → 매치 시작 → 배치 활성(프리뷰 추적 시작).
				modeCamera.gameObject.SetActive(true);
				ResetCamera();
				inputManager.SetInputStrategy(new InputStrategyTowerDefense(placement, inputManager));
				match.Begin(stage, stageRoot);
				placement.Activate();
				TowerDefenseHudView view = EnsureHud();
				if (view != null)
				{
					view.Show(stage);
					// 핫바 선택 표시 ↔ 실제 배치 대상은 같은 소스여야 한다(표시가 거짓말하면 오설치).
					view.SetSelectedKind(placement.SelectedKind);
					placement.SelectionChanged += view.SetSelectedKind;
					view.RestartRequested += Restart;
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
				}
				placement.Deactivate();
				hud?.Hide();
				modeCamera.gameObject.SetActive(false);
				inputManager.SetInputStrategy(new InputStrategyWorld());
			}
		}
	}
}
