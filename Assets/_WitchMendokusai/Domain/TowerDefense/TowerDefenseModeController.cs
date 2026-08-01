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
			UpdateCameraPan();
		}

		/// <summary>
		/// 개척 카메라 이동(WASD) — `InputAxisType.CameraMove` 축 재사용.
		/// CityViewCameraController/FreeFlyCameraController 와 같은 축이라 조작이 게임 전체에서 일관된다
		/// (플레이어 Move 축과는 분리돼 있어 이 모드에서 캐릭터가 움직일 위험 0).
		/// 스테이지 중심 기준으로 가둬 개척지를 화면 밖으로 잃어버리지 않게 한다.
		/// </summary>
		private void UpdateCameraPan()
		{
			if (modeCamera == null || stageRoot == null || stage == null || inputManager == null)
				return;

			Vector2 move = inputManager.CameraMoveInput;
			if (move.sqrMagnitude <= 0f)
				return;

			Vector3 delta = new Vector3(move.x, 0f, move.y) * stage.CameraPanSpeed * Time.deltaTime;
			Vector3 next = modeCamera.transform.position + delta;

			// 스테이지 중심 기준 XZ 클램프(카메라 높이·각도는 유지).
			Vector3 center = stageRoot.position;
			float limit = stage.CameraPanLimit;
			next.x = Mathf.Clamp(next.x, center.x - limit, center.x + limit);
			next.z = Mathf.Clamp(next.z, center.z - limit, center.z + limit);

			modeCamera.transform.position = next;
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
				}
			}
			else
			{
				// 이탈 — 매치 정리(멱등 Dispose) → 배치 비활성 → 모드 카메라 끄기 → 월드 입력 복귀.
				match.Dispose();
				if (hud != null)
					placement.SelectionChanged -= hud.SetSelectedKind;
				placement.Deactivate();
				hud?.Hide();
				modeCamera.gameObject.SetActive(false);
				inputManager.SetInputStrategy(new InputStrategyWorld());
			}
		}
	}
}
