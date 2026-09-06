using System.Collections;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
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
	///   스왑하지 않도록 <see cref="ModeControllerEdgeTrigger"/> 로 전이만 감지.
	/// ★ 모드 카메라 = **정식 content 카메라**(ContentCameraMode.TowerDefense vcam, priority 전환).
	///   구 구현은 본편 카메라 *위에* 별도 Camera 를 덧대 렌더했는데, 그러면 밑에서 본편이 계속 돌고
	///   화면 기준 카메라가 둘로 갈라진다 — 게임 속 게임이라도 **진입한 순간 그 게임이 주체**여야 한다
	///   (사용자 지시). 월드→화면 변환이 숨은 본편 카메라를 잡아 데미지 숫자가 엉뚱한 데 뜨던 것이 그 증상.
	/// </summary>
	public partial class TowerDefenseModeController : MonoBehaviour
	{
		public static TowerDefenseModeController Instance { get; private set; }

		public static bool TryGetExistingInstance(out TowerDefenseModeController controller)
		{
			controller = Instance;
			return controller != null;
		}

		private GameModeManager gameModeManager;
		private InputManager inputManager;
		// 월드 조작 복귀는 선택기 몫 (월드 전략을 짓는 자리 하나)
		private InputStrategySelector inputStrategySelector;
		private UIRoot uiRoot;

		// HUD = 평범한 클래스(MonoBehaviour X) — [Inject] 메서드 타입당 1개 제약 회피. 최초 진입 시 lazy 생성.
		private TowerDefenseHudView hud;

		[SerializeField] private TowerDefenseMatch match;
		[SerializeField] private TowerDefensePlacement placement;
		[SerializeField] private TowerDefenseStageSO stage;

		/// <summary> 펼친 지도가 열려 있나(검증 전용) — 「안 열렸다」와 「열렸는데 비었다」는 원인이 다르다. </summary>
		public bool IsMapOpenForVerification => hud != null && hud.IsMapOpen;

		/// <summary> 펼친 지도를 연다(검증 전용) — 지도 위 점이 무엇으로 읽히는지는 열어야 잴 수 있다. </summary>
		public void OpenMapForVerification()
		{
			TowerDefenseHudView view = EnsureHud();
			if (view != null && view.IsMapOpen == false)
				view.ToggleMap();
		}

		/// <summary> 지금 도는 스테이지 — 검증 하네스가 판정 기준(수치)을 규칙에서 그대로 읽게 한다. </summary>
		public TowerDefenseStageSO Stage => stage;
		[SerializeField] private Transform stageRoot;

		// 전이 감지 + 「지금 이 모드인가」. 초기 Default 재적용 no-op + enter/exit 1회 보장.
		// 투기장 컨트롤러와 같은 물건을 쓴다(WM-196 단계 7).
		private readonly ModeControllerEdgeTrigger modeEdge = new();


		[Inject]
		public void Construct(GameModeManager gameModeManager, InputManager inputManager, UIRoot uiRoot, ObjectPoolManager objectPoolManager, TimeManager timeManager, InputStrategySelector inputStrategySelector)
		{
			this.inputStrategySelector = inputStrategySelector;
			this.gameModeManager = gameModeManager;
			this.inputManager = inputManager;
			this.uiRoot = uiRoot;
			// 매치는 이 프리팹의 자식이라 스코프가 직접 못 줌. 여기서 전달
			match.Construct(objectPoolManager, timeManager);
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
			// 연구로 새 칸이 열리면 그 즉시 입력도 알아야 한다 — 화면엔 떴는데 손이 못 고르면
			// 「연구했는데 아무 일도 없다」가 된다.
			match.SlotsChanged += SyncAvailableTowers;
			// ★ 성좌는 *화면보다 먼저* 붙는다. 예전엔 이 셋을 「처음 성좌를 열 때」 붙였는데,
			//   이어하기는 사람이 성좌를 열기 전에 일어난다 → 되돌릴 곳이 아무도 없어 저장에
			//   적힌 연구가 통째로 조용히 사라졌다. 규칙은 화면 유무와 무관해야 한다.
			match.ResearchReset += ResetResearchNodes;
			match.CollectResearch += CollectResearchNodes;
			match.RestoreResearch += RestoreResearchNodes;
			ApplyMode(gameModeManager.CurrentMode);
		}

		private void OnDestroy()
		{
			if (gameModeManager != null)
				gameModeManager.OnModeChanged -= OnGameModeChanged;
			if (match != null)
			{
				match.MatchEnded -= OnMatchEnded;
				match.SlotsChanged -= SyncAvailableTowers;
				match.ResearchReset -= ResetResearchNodes;
				match.CollectResearch -= CollectResearchNodes;
				match.RestoreResearch -= RestoreResearchNodes;
			}
			if (Instance == this)
				Instance = null;
		}

		private void OnGameModeChanged(GameMode mode) => ApplyMode(mode);

		private void OnMatchEnded(TowerDefenseOutcome outcome)
		{
			// ★ 성좌를 열어 둔 채 판이 끝날 수 있다(멈춰 있어도 이어하기·저장 경로로 끝날 수 있고,
			//   무엇보다 결말 화면이 그 뒤에 가려지면 「판이 끝난 줄도 모르는」 상태가 된다).
			//   먼저 닫는다 — 멈춤도 여기서 같이 풀린다. 지도·메뉴도 같은 이유로 함께 닫는다.
			CloseOverlays();

			match.RestoreTimeScale(); // 결말 화면에서 버튼을 눌러야 하므로 시간이 멈춰 있으면 안 된다.

			// 무한 모드 = 버틴 웨이브가 곧 점수 → 기록을 남기지 않으면 판이 끝나도 아무것도 안 남는다.
			// ★ 점수는 이제 「몇 웨이브」가 아니라 **버틴 시간 + 부순 둥지**다 — 실시간에서 웨이브는
			//   시계가 부르므로 가만히 있어도 오른다(잘한 것과 시간이 흐른 것이 구분되지 않는다).
			int score = TowerDefenseMeta.ScoreForRealtime(match.SurvivedSeconds, match.NestsDestroyed, stage.ScoreSecondsPerNest);
			int best = score;
			bool isNewRecord = false;
			if (DataManager.TryGetExistingInstance(out DataManager dataManager))
			{
				isNewRecord = TowerDefenseRecord.Submit(dataManager.FeatureSave<TowerDefenseSaveSlice>().BestWave, stage.ID, score, out best);
				if (isNewRecord)
					dataManager.SaveManager.SaveData();
			}

			// 판 밖에 남는 것 — 버틴 만큼 유물. 없으면 끝나도 최고 기록 숫자 하나뿐이라 다음 판이 안 달라진다.
			int relicsGained = TowerDefenseMeta.RelicsForRealtime(
				match.SurvivedSeconds, match.NestsDestroyed, stage.RelicsPerMinute, stage.RelicsPerNest, stage.RelicsBaseReward);
			if (DataManager.TryGetExistingInstance(out DataManager relicOwner))
			{
				relicOwner.FeatureSave<TowerDefenseSaveSlice>().Relics += relicsGained;
				relicOwner.SaveManager.SaveData();
			}

			// 끝난 판의 저장은 버린다 — 남겨두면 다음 진입이 「끝난 판」으로 되살아난다.
			if (DataManager.TryGetExistingInstance(out DataManager clearOwner))
			{
				clearOwner.FeatureSave<TowerDefenseSaveSlice>().Resume = null;
				clearOwner.SaveManager.SaveData();
			}

			Debug.Log($"{nameof(TowerDefenseModeController)}: 매치 종료 — outcome={outcome} 버틴시간={match.SurvivedSeconds}s 둥지={match.NestsDestroyed} 점수={score} best={best} newRecord={isNewRecord} relics+{relicsGained}");
			hud?.ShowOutcome(outcome, match.SurvivedSeconds, match.NestsDestroyed, score, best, match.LairsAwakened, match.LairsCleared, isNewRecord, relicsGained, RelicBalance(), CanPull(), match.BuildSummary());
		}

		/// <summary> 현재 스테이지 최고 기록 — 화면에 목표를 세워준다(없으면 0). </summary>
		private int CurrentBestRecord()
		{
			if (DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return 0;
			return TowerDefenseRecord.Best(dataManager.FeatureSave<TowerDefenseSaveSlice>().BestWave, stage.ID);
		}

		// HUD 갱신 + 카메라 이동 — TD 모드 동안만.
		private void Update()
		{
			if (modeEdge.IsActive == false)
				return;

			hud?.Tick(match, stage);

			// 커서가 얹힌 유닛 설명 — 배치가 찾은 대상을 그대로 쓴다(둘이 갈라지면 툴팁이 거짓말한다).
			hud?.ShowUnitTooltip(match.DescribeUnit(placement.HoveredUnit), placement.HoverScreenPosition.ToSim());

			// 사거리는 묻는 순간에만 — 얹힌 그 건물 하나만 켠다(상시 표시는 노이즈).
			match.HighlightRangeOf(placement.HoveredUnit != null ? placement.HoveredUnit.transform : null);

			RefreshSelectionPanel();

			// 지금 설치 대기인지 — 클릭 한 번의 뜻이 여기서 갈리므로 매 프레임 화면에 박는다.
			hud?.SetArmed(placement.IsArmed, DescribeSelectedSlot());
		}

		/// <summary>
		/// 월드에 붙는 UI 는 *카메라가 움직인 뒤에* 그린다.
		/// ★ 카메라(시네머신)는 LateUpdate 에 자리를 잡는다. Update 에서 그리면 한 프레임 전 카메라로
		///   좌표를 계산해, 화면을 밀 때마다 이름표가 뒤로 끌린다(사용자 실증: "WASD로 움직이면 특히 심하다").
		/// </summary>
		private void LateUpdate()
		{
			if (modeEdge.IsActive == false)
				return;
			hud?.TickWorldAnchored();
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

			Vector3 center = stageRoot != null ? stageRoot.position.ToSim() : Vector3.zero;
			overhead.SetFocusBounds(center.ToUnity(), stage.CameraPanLimit);
			overhead.ConfigureZoom(stage.CameraMinHeight, stage.CameraMaxHeight, stage.CameraZoomSpeed);
			overhead.ResetView(center.ToUnity(), yaw: 0f, height: stage.CameraInitialHeight);
		}

		/// <summary>
		/// 이번 판에 쓸 수 있는 칸을 배치 입력에 알린다 — 목록의 주인은 매치다(둘이 어긋나면 오설치).
		///
		/// ★ 예전엔 여기서 「뽑은 포탑 종류」를 따로 조립해 넘겼다. 이제 해금은 *연구 단계*가 정하므로
		///   그 계산이 두 벌이 되면 곧바로 어긋난다 — 매치가 만든 목록을 그대로 전달하기만 한다.
		/// </summary>
		private void SyncAvailableTowers()
		{
			if (match == null || placement == null)
				return;
			placement.SetSlots(match.AvailableSlots);
		}

		/// <summary> 시점을 그 자리로 — 지도에서 온 요청. 확대·회전은 그대로 둔다. </summary>
		private void LookAt(Vector3 focus)
		{
			// 카메라는 모드마다 다른 리그가 쥔다 — 개척 리그를 그때그때 찾는다(참조를 들고 있으면
			// 모드를 나갔다 들어올 때 죽은 참조가 된다).
			if (OverheadContentCameraController.TryGet(ContentCameraMode.TowerDefense, out OverheadContentCameraController rig))
				rig.LookAt(focus.ToUnity());
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
			if (modeEdge.IsActive == false)
				return;

			// 버튼을 누른 그 클릭이 배치로도 새는 것을 **즉시** 삼킨다(코루틴 안에서 하면 같은 프레임에 늦는다).
			placement.SuppressNextClick();

			// ★ 덮고 있던 창을 걷는다 — 다시 시작했는데 성좌·지도·메뉴가 그대로면 새 판의 첫 화면이
			//   그 창이고, 메뉴가 멈춰 둔 판이면 시간까지 멈춘 채 시작한다.
			//   (다시 시작은 결말 화면에서도 눌린다 — 그때 메뉴가 열려 있는 경우가 실제로 흔하다.)
			CloseOverlays();

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
			// 새 판의 첫 칸 — 포탑은 이제 연구로 열리므로 시작 시점엔 없다(없는 칸을 고르려 하면
			// 아무 칸도 안 골린 채로 판이 시작된다).
			placement.SelectSlot(0);
			ResetCamera();

			TowerDefenseHudView view = EnsureHud();
			if (view != null)
			{
				// Show 가 아니라 전용 리셋 — Show 는 본편 UI 를 다시 숨기며 복원 정보를 덮어쓴다(이미 숨긴 상태라 빈 목록이 됨).
				SyncAvailableTowers();
				view.ResetForNewMatch(stage);
				view.SetBestRecord(CurrentBestRecord());
				view.SetSelectedSlot(placement.SelectedSlot);
			}

			Debug.Log($"{nameof(TowerDefenseModeController)}: 개척 재시작 — 새 매치 시작.");
		}

		/// <summary>
		/// 지금 화면(없으면 null) — 확인 도구가 *실제로 화면에 뜬 것*을 재기 위해 연다.
		/// ★ 툴팁처럼 마우스가 있어야 뜨는 것은 하네스가 띄울 손잡이가 없으면 영영 미측정으로 남는다.
		/// </summary>
		public TowerDefenseHudView Hud => hud;

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

			if (modeEdge.Crossed(isTowerDefense) == false)
				return; // 전이 아님 — 전략 스왑/매치 토글 생략(셀렉터 레이스·중복 방지).

			if (isTowerDefense)
			{
				// 진입 — content 카메라 전환(개척 vcam 승격)은 CameraManager 단일 권위자가 GameMode 를 보고
				// 이미 처리한다. 여기서는 **무대가 아는 것**(시점 위치·경계·줌 범위)만 맞춘다.
				ResetCamera();
				inputManager.SetInputStrategy(new InputStrategyTowerDefense(placement, inputManager, match,
					() => EnsureHud()?.ToggleMap(),
					CancelPressed));

				// ★ 나갈 때 저장해 두고 *아무도 읽지 않던* 것을 여기서 읽는다 — 저장만 하고 이어하기가 없으면
				//   「잠깐 접어둔다」가 그냥 「버린다」였다. 씨앗까지 넘겨야 같은 땅이 다시 나오므로 Begin 직전.
				if (DataManager.TryGetExistingInstance(out DataManager resumeOwner)
					&& resumeOwner.FeatureSave<TowerDefenseSaveSlice>().Resume != null
					&& resumeOwner.FeatureSave<TowerDefenseSaveSlice>().Resume.IsResumable)
				{
					match.RestoreSave(resumeOwner.FeatureSave<TowerDefenseSaveSlice>().Resume);
				}

				match.Begin(stage, stageRoot);
				placement.Activate();
				TowerDefenseHudView view = EnsureHud();
				if (view != null)
				{
					SyncAvailableTowers();
					view.Show(stage);
					view.SetBestRecord(CurrentBestRecord()); // 넘어야 할 선을 판 시작부터 보여준다.
					// 핫바 선택 표시 ↔ 실제 배치 대상은 같은 소스여야 한다(표시가 거짓말하면 오설치).
					view.SetSelectedSlot(placement.SelectedSlot);
					placement.SelectionChanged += view.SetSelectedSlot;
					view.RestartRequested += Restart;
					// 지도·미니맵을 누르면 그 자리로 — 카메라는 컨트롤러가 쥔다(화면은 「어디」만 말한다).
					view.LookAtRequested += LookAt;
					view.ResearchPanelRequested += OpenResearch;
					view.SellSelectedRequested += SellSelected;
					view.ExitRequested += () => gameModeManager.SetMode(GameMode.Default);
					view.SelectionCloseRequested += CloseSelection;
					view.MenuResumeRequested += ResumeFromMenu;
					view.MenuToggleRequested += ToggleMenu;
					view.WaveModeToggleRequested += ToggleWaveMode;
					view.NextWaveRequested += CallNextWave;
					view.SlotClicked += SelectSlotFromUi;
					view.PullRequested += PullTower;
					view.PauseToggleRequested += match.TogglePause;
					view.SpeedCycleRequested += match.CycleSpeed;
					view.ToggleAllRangesRequested += ToggleAllRanges;
					view.UiScaleCycleRequested += CycleUiScale;
					view.BuildingPerkChosen += ChoosePerk;
					view.CoreCardChosen += ChooseCoreCard;
					view.DifficultyCycleRequested += CycleDifficulty;
				}
			}
			else
			{
				// ★ 나가기 전에 저장한다 — 판 도중에 나가는 것이 곧 「잠깐 접어둔다」가 되어야 한다.
				//   끝난 판은 저장하지 않는다(CaptureSave 가 null 을 준다) — 끝난 것을 이어하면 거짓말이다.
				if (DataManager.TryGetExistingInstance(out DataManager saveOwner))
				{
					saveOwner.FeatureSave<TowerDefenseSaveSlice>().Resume = match.CaptureSave();
					saveOwner.SaveManager.SaveData();
				}

				// ★ 성좌를 열어 둔 채 나갈 수 있다. 안 닫으면 다음에 개척에 들어왔을 때 **첫 화면이
				//   성좌**이고, 게다가 그때 판이 멈춰 있다(성좌가 멈춘 것을 성좌가 풀어야 하는데
				//   그 닫는 손이 안 왔다). 나가는 자리에서 닫는다 — 멈춤도 여기서 같이 풀린다.
				CloseOverlays();

				// 이탈 — 매치 정리(멱등 Dispose) → 배치 비활성 → 모드 카메라 끄기 → 월드 입력 복귀.
				StopAllCoroutines(); // 재시작 코루틴이 이탈 뒤 재개해 매치를 되살리는 것 차단.
				match.RestoreTimeScale(); // 멈춘 채로 나가면 본편이 정지한다.
				match.Dispose();
				if (hud != null)
				{
					placement.SelectionChanged -= hud.SetSelectedSlot;
					hud.RestartRequested -= Restart;
					hud.LookAtRequested -= LookAt;
					hud.ResearchPanelRequested -= OpenResearch;
					hud.SellSelectedRequested -= SellSelected;
					hud.SelectionCloseRequested -= CloseSelection;
				hud.MenuResumeRequested -= ResumeFromMenu;
				hud.MenuToggleRequested -= ToggleMenu;
					hud.WaveModeToggleRequested -= ToggleWaveMode;
					hud.NextWaveRequested -= CallNextWave;
					hud.SlotClicked -= SelectSlotFromUi;
					hud.PullRequested -= PullTower;
					hud.PauseToggleRequested -= match.TogglePause;
					hud.SpeedCycleRequested -= match.CycleSpeed;
					hud.ToggleAllRangesRequested -= ToggleAllRanges;
					hud.UiScaleCycleRequested -= CycleUiScale;
					hud.BuildingPerkChosen -= ChoosePerk;
					hud.CoreCardChosen -= ChooseCoreCard;
					hud.DifficultyCycleRequested -= CycleDifficulty;
				}
				placement.Deactivate();
				hud?.Hide();
				inputStrategySelector.RestoreWorldStrategy();
			}
		}
	}
}
