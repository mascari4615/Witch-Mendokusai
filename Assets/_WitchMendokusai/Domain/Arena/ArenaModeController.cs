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
	///   World 전략 세팅)와 레이스/중복 스왑하지 않도록 <see cref="ModeControllerEdgeTrigger"/> 로 전이만 감지.
	/// ★ 관전 카메라 = **정식 content 카메라**(ContentCameraMode.Arena vcam, priority 전환) — TASK-WM-194 근본 수정.
	///   구 구현은 본편 카메라 *위에* 별도 Camera 를 덧대 렌더했다("추종 궤도라 안 맞는다"는 당시 판단은
	///   자유 위치 카메라(CityView/FreeFly/개척)가 생기면서 낡았다). 덧대는 방식은 밑에서 본편이 계속 돌아
	///   화면 기준 카메라가 둘로 갈라지고, 월드→화면 변환이 숨은 본편 카메라를 잡는다(데미지 숫자 오배치가 그 증상).
	///   게임 속 게임이라도 **진입한 순간 그 게임이 주체**여야 한다(사용자 지시).
	/// </summary>
	public class ArenaModeController : MonoBehaviour
	{
		public static ArenaModeController Instance { get; private set; }

		public static bool TryGetExistingInstance(out ArenaModeController controller)
		{
			controller = Instance;
			return controller != null;
		}

		/// <summary>
		/// 지금 매치가 실제로 돌고 있나 — 검증 하네스가 「관전 화면은 떴는데 매치는 안 돌더라」를
		/// *있는 신호*로 말할 수 있게 연다. 없으면 읽는 쪽이 **없는 줄(MATCH-START)을 알아채야** 하는데,
		/// 그건 「no-news is bad-news」라 사람이 놓친다.
		/// </summary>
		public bool IsMatchRunning => arenaMatch != null && arenaMatch.IsRunning;

		private GameModeManager gameModeManager;
		private InputManager inputManager;

		[SerializeField] private ArenaMatch arenaMatch;

		// 전이 감지 — 초기 Default 재적용 no-op + enter/exit 1회 보장. 개척 컨트롤러와 같은 물건을 쓴다(WM-196 단계 7).
		private readonly ModeControllerEdgeTrigger modeEdge = new();
		// 관전 카메라는 씬에 있을 때만 (RegisterInHierarchyIfPresent). 전략을 만들 때 물어봄
		private IObjectResolver resolver;
		private InputStrategySelector inputStrategySelector;

		[Inject]
		public void Construct(GameModeManager gameModeManager, InputManager inputManager, ObjectPoolManager objectPoolManager, TimeManager timeManager, IObjectResolver resolver, InputStrategySelector inputStrategySelector)
		{
			this.gameModeManager = gameModeManager;
			this.inputManager = inputManager;
			this.resolver = resolver;
			this.inputStrategySelector = inputStrategySelector;
			// 매치는 이 프리팹의 자식이라 스코프가 직접 못 줌. 여기서 전달
			arenaMatch.Construct(objectPoolManager, timeManager);
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

			if (modeEdge.Crossed(isArena) == false)
				return; // 전이 아님 — 전략 스왑/매치 토글 생략(셀렉터 레이스·중복 방지).

			if (isArena)
			{
				// 진입 — content 카메라 전환(투기장 vcam 승격)은 CameraManager 단일 권위자가 GameMode 를
				// 보고 처리한다. 여기서는 입력·매치만.
				resolver.TryResolve(out CameraManager cameraManager);
				inputManager.SetInputStrategy(new InputStrategyArena(cameraManager, gameModeManager));
				arenaMatch.Begin();

				// ★ `Begin` 은 검증(config 미할당 / 로스터 TeamId / 팀당 유닛 수 / 스폰 겹침)에 걸리면
				//   **LogError 만 남기고 조용히 돌아온다.** 그런데 이 시점엔 모드·카메라·입력이 이미
				//   투기장으로 바뀐 뒤다 → 화면은 「관전 시점인데 아무도 없는 빈 판」이 되고,
				//   원인은 콘솔 위쪽 ArenaMatch 에러 한 줄뿐이라 사람은 **화면이 고장난 줄 안다.**
				//   되돌리진 않는다(모드 변경 핸들러 안에서 모드를 되돌리면 재진입이 된다) —
				//   대신 무슨 일이 일어났는지 이름을 붙인다. 개발자 런처(ArenaTestLauncher)엔 같은
				//   가드가 이미 있는데 **플레이어 경로인 여기만 비어 있었다.**
				if (arenaMatch.IsRunning == false)
				{
					Debug.LogError($"{nameof(ArenaModeController)}: MATCH-NOT-STARTED — 투기장 모드로 들어왔지만 "
						+ "Begin 이 거절했다(빈 판이 뜬다). 바로 위 ArenaMatch LogError 를 볼 것 — "
						+ "config/arenaRoot 미할당 · 로스터 TeamId 범위 · 팀당 유닛 수 > 맵 스폰 · 스폰 겹침 중 하나다.");
				}
			}
			else
			{
				// 이탈 — 매치 정리(멱등 Dispose) → 월드 입력 복귀(카메라 복귀는 단일 권위자 담당).
				arenaMatch.Dispose();
				inputStrategySelector.RestoreWorldStrategy();
			}
		}
	}
}
