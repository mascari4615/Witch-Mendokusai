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

		[SerializeField] private TowerDefenseMatch match;
		[SerializeField] private TowerDefensePlacement placement;
		[SerializeField] private TowerDefenseStageSO stage;
		[SerializeField] private Transform stageRoot;
		[Tooltip("개척 모드 카메라 — 진입 시 활성(depth 높아 플레이어 카메라 위 풀스크린 렌더). 평범한 Camera, ContentCameraMode 리그 아님.")]
		[SerializeField] private Camera modeCamera;

		// 전이 감지 — 직전 적용이 TD 모드였는지. 초기 Default 재적용 no-op + enter/exit 1회 보장.
		private bool wasTowerDefense;

		[Inject]
		public void Construct(GameModeManager gameModeManager, InputManager inputManager)
		{
			this.gameModeManager = gameModeManager;
			this.inputManager = inputManager;
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

		// UI 는 후속 증분 — 지금은 결과 확인용 로그만(콘솔 ground-truth).
		private void OnMatchEnded(TowerDefenseOutcome outcome)
		{
			Debug.Log($"{nameof(TowerDefenseModeController)}: 매치 종료 — outcome={outcome}");
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
			}
			else
			{
				// 이탈 — 매치 정리(멱등 Dispose) → 배치 비활성 → 모드 카메라 끄기 → 월드 입력 복귀.
				match.Dispose();
				placement.Deactivate();
				modeCamera.gameObject.SetActive(false);
				inputManager.SetInputStrategy(new InputStrategyWorld());
			}
		}
	}
}
