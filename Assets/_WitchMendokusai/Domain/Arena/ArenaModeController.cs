using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 투기장 모드 컨트롤러 (TASK-WM-165 item9) — BuildManager 미러. GameMode.Arena 진입/이탈을
	/// 단일 ApplyMode 로 처리: 진입 = 관전 카메라 + 관전 입력 전략 + 매치 시작 / 이탈 = 매치 정리 +
	/// 일반 카메라 + 월드 입력 전략. 모드 토글은 GameModeManager.SetMode(Arena) 가 트리거(인게임 진입점).
	///
	/// ★ 엣지 트리거 — Build 와 달리 전략 스왑(InputManager.SetInputStrategy)·매치 Begin/Dispose 는
	///   상태 전이(enter/exit)에서만 1회. 초기 Start(Default) 재적용이 InputStrategySelector(씬 로드 시
	///   World 전략 세팅)와 레이스/중복 스왑하지 않도록 wasArena 로 전이만 감지.
	/// ⚠ 관전 카메라(cameras[Arena=3]) 씬 배치는 Phase B(에디터). 미배치 상태에선 SetContentCameraMode(Arena)
	///   가 잘못된/없는 카메라를 가리킴 — 코드 정합은 유지, 씬 배선 후 동작 완결.
	/// </summary>
	public class ArenaModeController : MonoBehaviour
	{
		public static ArenaModeController Instance { get; private set; }

		public static bool TryGetExistingInstance(out ArenaModeController controller)
		{
			controller = Instance;
			return controller != null;
		}

		private GameModeManager gameModeManager;
		private CameraManager cameraManager;
		private InputManager inputManager;

		[SerializeField] private ArenaMatch arenaMatch;

		// 전이 감지 — 직전 적용이 투기장 모드였는지. 초기 Default 재적용 no-op + enter/exit 1회 보장.
		private bool wasArena;

		[Inject]
		public void Construct(GameModeManager gameModeManager, CameraManager cameraManager, InputManager inputManager)
		{
			this.gameModeManager = gameModeManager;
			this.cameraManager = cameraManager;
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
			ApplyMode(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;
			if (Instance == this)
				Instance = null;
		}

		private void OnGameModeChanged(GameMode mode) => ApplyMode(mode);

		private void ApplyMode(GameMode mode)
		{
			bool isArena = mode == GameMode.Arena;

			if (isArena == wasArena)
				return; // 전이 아님 — 전략 스왑/매치 토글 생략(셀렉터 레이스·중복 방지).

			wasArena = isArena;

			if (isArena)
			{
				// 진입 — 관전 카메라 → 관전 입력(이동·전투 차단) → 매치 시작.
				cameraManager.SetContentCameraMode(ContentCameraMode.Arena);
				inputManager.SetInputStrategy(new InputStrategyArena());
				arenaMatch.Begin();
			}
			else
			{
				// 이탈 — 매치 정리(멱등 Dispose) → 일반 카메라 → 월드 입력 복귀.
				arenaMatch.Dispose();
				cameraManager.SetContentCameraMode(ContentCameraMode.Normal);
				inputManager.SetInputStrategy(new InputStrategyWorld());
			}
		}
	}
}
