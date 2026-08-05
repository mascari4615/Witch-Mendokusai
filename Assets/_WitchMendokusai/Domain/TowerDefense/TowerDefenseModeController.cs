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
			// 연구로 새 칸이 열리면 그 즉시 입력도 알아야 한다 — 화면엔 떴는데 손이 못 고르면
			// 「연구했는데 아무 일도 없다」가 된다.
			match.SlotsChanged += SyncAvailableTowers;
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
			match.RestoreTimeScale(); // 결말 화면에서 버튼을 눌러야 하므로 시간이 멈춰 있으면 안 된다.

			// 무한 모드 = 버틴 웨이브가 곧 점수 → 기록을 남기지 않으면 판이 끝나도 아무것도 안 남는다.
			// ★ 점수는 이제 「몇 웨이브」가 아니라 **버틴 시간 + 부순 둥지**다 — 실시간에서 웨이브는
			//   시계가 부르므로 가만히 있어도 오른다(잘한 것과 시간이 흐른 것이 구분되지 않는다).
			int score = TowerDefenseMeta.ScoreForRealtime(match.SurvivedSeconds, match.NestsDestroyed, stage.ScoreSecondsPerNest);
			int best = score;
			bool isNewRecord = false;
			if (DataManager.TryGetExistingInstance(out DataManager dataManager))
			{
				isNewRecord = TowerDefenseRecord.Submit(dataManager.TowerDefenseBestWave, stage.ID, score, out best);
				if (isNewRecord)
					dataManager.SaveManager.SaveData();
			}

			// 판 밖에 남는 것 — 버틴 만큼 유물. 없으면 끝나도 최고 기록 숫자 하나뿐이라 다음 판이 안 달라진다.
			int relicsGained = TowerDefenseMeta.RelicsForRealtime(
				match.SurvivedSeconds, match.NestsDestroyed, stage.RelicsPerMinute, stage.RelicsPerNest, stage.RelicsBaseReward);
			if (DataManager.TryGetExistingInstance(out DataManager relicOwner))
			{
				relicOwner.TowerDefenseRelics += relicsGained;
				relicOwner.SaveManager.SaveData();
			}

			// 끝난 판의 저장은 버린다 — 남겨두면 다음 진입이 「끝난 판」으로 되살아난다.
			if (DataManager.TryGetExistingInstance(out DataManager clearOwner))
			{
				clearOwner.TowerDefenseResume = null;
				clearOwner.SaveManager.SaveData();
			}

			Debug.Log($"{nameof(TowerDefenseModeController)}: 매치 종료 — outcome={outcome} 버틴시간={match.SurvivedSeconds}s 둥지={match.NestsDestroyed} 점수={score} best={best} newRecord={isNewRecord} relics+{relicsGained}");
			hud?.ShowOutcome(outcome, match.SurvivedSeconds, match.NestsDestroyed, score, best, isNewRecord, relicsGained, RelicBalance(), CanPull(), match.BuildSummary());
		}

		/// <summary> 현재 스테이지 최고 기록 — 화면에 목표를 세워준다(없으면 0). </summary>
		private int CurrentBestRecord()
		{
			if (DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return 0;
			return TowerDefenseRecord.Best(dataManager.TowerDefenseBestWave, stage.ID);
		}

		private readonly System.Collections.Generic.List<TowerDefenseBuildingPerk> perkOffers = new();
		private readonly System.Collections.Generic.List<TowerDefenseBoon> coreCards = new();

		// HUD 갱신 + 카메라 이동 — TD 모드 동안만.
		private void Update()
		{
			if (wasTowerDefense == false)
				return;

			hud?.Tick(match, stage);

			// 커서가 얹힌 유닛 설명 — 배치가 찾은 대상을 그대로 쓴다(둘이 갈라지면 툴팁이 거짓말한다).
			hud?.ShowUnitTooltip(match.DescribeUnit(placement.HoveredUnit), placement.HoverScreenPosition);

			// 사거리는 묻는 순간에만 — 얹힌 그 건물 하나만 켠다(상시 표시는 노이즈).
			match.HighlightRangeOf(placement.HoveredUnit != null ? placement.HoveredUnit.transform : null);

			RefreshSelectionPanel();

			// 지금 설치 대기인지 — 클릭 한 번의 뜻이 여기서 갈리므로 매 프레임 화면에 박는다.
			hud?.SetArmed(placement.IsArmed, DescribeSelectedSlot());
		}

		/// <summary> 지금 고른 칸이 무엇인가 — 설치 대기 표시에 쓴다. </summary>
		private string DescribeSelectedSlot()
		{
			if (match == null || placement == null)
				return string.Empty;

			System.Collections.Generic.IReadOnlyList<TowerDefenseSlot> slots = match.AvailableSlots;
			int index = placement.SelectedSlot;
			if (index < 0 || index >= slots.Count)
				return string.Empty;

			return slots[index].Kind switch
			{
				TowerDefensePlaceableKind.Harvester => "채집 인형",
				TowerDefensePlaceableKind.Wall => "벽",
				TowerDefensePlaceableKind.Trap => "함정",
				TowerDefensePlaceableKind.Outpost => "전초기지",
				TowerDefensePlaceableKind.Generator => "발전 인형",
				TowerDefensePlaceableKind.Hero => "영웅 부르기",
				_ => "포탑 인형",
			};
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

		/// <summary>
		/// 연구 창 열기 — 코어를 골라 준다.
		///
		/// ★ 왜 버튼이 필요한가: 연구는 「코어를 클릭」해야 열리는데, 그 사실이 화면 어디에도 없었다
		///   (사용자 실증: "연구 어케 여는데"). 첫 판의 *유일한 다음 수*가 숨은 문 뒤에 있으면
		///   게임이 시작되지 않는다. 코어 클릭은 그대로 두고, 눈에 보이는 문을 하나 더 낸다.
		/// </summary>
		// 연구 성좌 — 전체화면 그래프(사용자 지시). 처음 열 때 한 번 세운다.
		private TowerDefenseResearchView researchView;

		private void OpenResearch()
		{
			if (match == null || match.CoreCombatant == null)
				return;

			placement.SuppressNextClick(); // 이 클릭이 지면 설치로 새지 않게.

			if (researchView == null && uiRoot != null && uiRoot.ModeHudLayer != null)
			{
				researchView = new TowerDefenseResearchView();
				// 모양은 스테이지가 정한다 — 갈래 수·길이·주는 양 전부 인스펙터에서.
				researchView.Build(uiRoot.ModeHudLayer, stage.ResearchBranchCount, stage.ResearchRingCount,
					stage.ResearchMajorAmount, stage.ResearchMinorAmount);
				researchView.NodeChosen += OnResearchNodeChosen;
			}

			researchView?.SetOpen(true);
		}

		/// <summary> 성좌에서 마디를 찍었다 — 값·효과는 규칙층이 정한다(화면은 고르기만 한다). </summary>
		private void OnResearchNodeChosen(int nodeId)
		{
			if (match == null || researchView == null)
				return;
			if (researchView.TryGetNode(nodeId, out TowerDefenseResearchGraph.Node node) == false)
				return;

			// 값을 못 치르면 화면에서도 도로 지운다 — 「찍힌 척」이 남으면 다음 마디가 잘못 열린다.
			if (match.TryTakeResearchNode(node.Effect, node.Amount, node.Cost) == false)
				researchView.Undo(nodeId);
		}

		/// <summary> 시점을 그 자리로 — 지도에서 온 요청. 확대·회전은 그대로 둔다. </summary>
		private void LookAt(Vector3 focus)
		{
			// 카메라는 모드마다 다른 리그가 쥔다 — 개척 리그를 그때그때 찾는다(참조를 들고 있으면
			// 모드를 나갔다 들어올 때 죽은 참조가 된다).
			if (OverheadContentCameraController.TryGet(ContentCameraMode.TowerDefense, out OverheadContentCameraController rig))
				rig.LookAt(focus);
		}

		/// <summary> X — 열린 창을 닫는다. 판을 나가지는 않는다(잘못 누르면 판이 통째로 끝난다). </summary>
		private void CancelPressed()
		{
			TowerDefenseHudView view = EnsureHud();
			if (view == null)
				return;

			// ★ 취소 키의 뜻 = 「지금 열린 것을 닫는다. 닫을 게 없으면 메뉴를 연다」 (TASK-WM-200).
			//   사용자 지시("X 로 게임 탈출 안 되게 · ESC 로 메뉴창")를 한 규칙으로 만족시킨다 —
			//   취소가 곧 판 끝내기였던 예전 동작은 되돌릴 수 없는 일이 가장 누르기 쉬운 자리에 있던 것이다.
			if (view.IsMenuOpen)
			{
				ToggleMenu();
				return;
			}

			// 성좌가 전체화면을 덮고 있으면 그것부터 닫는다 — 덮은 것을 두고 뒤의 것을 닫으면 안 된다.
			if (researchView != null && researchView.IsOpen)
			{
				researchView.SetOpen(false);
				return;
			}

			if (view.IsMapOpen)
			{
				view.ToggleMap();
				return;
			}

			// ★ 짓기를 무르는 자리 (사용자 실측: "건물 짓기 취소는 어케함? 할 구가 없네").
			//   칸을 고르면 「설치 대기」가 켜지는데, 그걸 *끄는 손잡이가 어디에도 없었다* —
			//   마음이 바뀌면 아무 데나 지어서 부수거나 판을 나가는 수밖에 없었다.
			//   지도·메뉴보다 뒤, 고른 건물 닫기보다 앞 — 「지금 손에 든 것」이 가장 먼저 놓여야 한다.
			if (placement != null && placement.IsArmed)
			{
				placement.Disarm();
				hud?.SetArmed(false, DescribeSelectedSlot());
				return;
			}

			if (placement != null && placement.SelectedBuilding != null)
			{
				CloseSelection();
				return;
			}

			// 닫을 것이 없다 — 메뉴를 연다.
			ToggleMenu();
		}

		/// <summary>
		/// 메뉴 여닫기 단일 창구 — 메뉴와 멈춤은 한 몸이라 한 곳에서만 다룬다
		/// (따로 두면 「메뉴는 떠 있는데 판은 계속 돈다」가 생긴다).
		/// </summary>
		private void ToggleMenu()
		{
			TowerDefenseHudView view = EnsureHud();
			if (view == null)
				return;

			if (view.IsMenuOpen)
			{
				view.SetMenuOpen(false);
				ResumeFromMenu();
				return;
			}

			view.SetMenuOpen(true);
			// 메뉴를 보는 동안 코어가 깨지면 안 된다.
			if (match != null && match.IsPaused == false)
			{
				pausedByMenu = true;
				match.TogglePause();
			}
		}

		// 메뉴가 멈춘 판인지 — 메뉴 때문에 멈춘 것만 메뉴가 다시 풀어야 한다(사용자가 직접 멈춰 뒀으면 그대로).
		private bool pausedByMenu;

		private void ResumeFromMenu()
		{
			if (pausedByMenu == false)
				return;
			pausedByMenu = false;
			if (match != null && match.IsPaused)
				match.TogglePause();
		}

		/// <summary> 고른 건물을 판다 — 창에서 바로(손이 규칙을 기억하지 않게). </summary>
		private void SellSelected()
		{
			ArenaCombatant selected = placement.SelectedBuilding;
			if (match == null || selected == null)
				return;

			placement.SuppressNextClick();
			match.TrySell(selected.Position, stage != null ? stage.SellRefundRatio : 0.6f);
			CloseSelection();
		}

		/// <summary> 창 닫기 — 고른 것을 놓는다. </summary>
		private void CloseSelection()
		{
			placement.SuppressNextClick();
			placement.SelectBuilding(null);
			RefreshSelectionPanel();
		}

		private int RelicBalance()
		{
			return DataManager.TryGetExistingInstance(out DataManager dataManager) ? dataManager.TowerDefenseRelics : 0;
		}

		private bool CanPull()
		{
			if (stage == null || DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return false;

			return dataManager.TowerDefenseRelics >= stage.PullCost
				&& TowerDefenseMeta.HasLockedTower(
					stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0,
					stage.DefaultUnlockedTowerCount,
					dataManager.TowerDefenseUnlockedTowers);
		}

		/// <summary>
		/// 인형 뽑기 — 결말 화면에서 바로. 별도 창을 새로 세우지 않는 이유: 뽑는 순간은 판이 끝난 직후이고,
		/// 그 자리에서 「다음 판엔 이게 있다」로 이어져야 다시 도전할 이유가 그 화면 안에서 닫힌다.
		/// </summary>
		private void PullTower()
		{
			if (stage == null || DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return;

			int relics = dataManager.TowerDefenseRelics;
			bool pulled = TowerDefenseMeta.TryPull(
				stage.TowerArchetypes != null ? stage.TowerArchetypes.Length : 0,
				stage.DefaultUnlockedTowerCount,
				dataManager.TowerDefenseUnlockedTowers,
				ref relics,
				stage.PullCost,
				UnityEngine.Random.value,
				out int pulledIndex);

			if (pulled == false)
				return;

			dataManager.TowerDefenseRelics = relics;
			dataManager.SaveManager.SaveData();

			TowerDefenseTowerArchetype pulledTower = match.TowerArchetypeAt(pulledIndex);
			Debug.Log($"{nameof(TowerDefenseModeController)}: 인형 뽑기 — {(pulledTower != null ? pulledTower.DisplayName : pulledIndex.ToString())} 획득 (유물 {relics} 남음)");
			hud?.ShowPullResult(pulledTower, relics, CanPull());
		}

		/// <summary> 코어 레벨업 카드 선택 — 판 전체에 걸린다. </summary>
		private void ChooseCoreCard(int index)
		{
			placement.SuppressNextClick();
			match.ChooseCoreCard(index);
		}

		/// <summary>
		/// 고른 건물의 선택창 — 강화 선택지 / 코어 카드 / 연구 버튼이 여기서 뜬다.
		///
		/// ★ 이게 없어서 그 셋이 전부 *코드만 있고 한 번도 안 떴다*(라이브 측정으로 드러남 —
		///   화면 조각을 세어보니 선택창이 목록에 아예 없었다). 「건물 선택하면 그때 띄운다」가
		///   사용자가 요청한 모양이므로, 고른 대상이 바뀔 때마다 그 대상 기준으로 다시 그린다.
		/// ★ 매 프레임 부르지만 화면은 *개수가 바뀔 때만* 다시 그린다(그쪽에 못 박혀 있다) —
		///   여기서 미리 걸러내면 「무엇이 바뀌었나」 판정이 두 곳에 갈라진다.
		/// </summary>
		private void RefreshSelectionPanel()
		{
			if (hud == null)
				return;

			ArenaCombatant selected = placement.SelectedBuilding;
			if (selected == null || selected.IsAlive == false)
			{
				hud.ShowSelection(null, canResearch: false, researchLevel: 0, researchCost: 0);
				return;
			}

			bool isCore = match.CoreCombatant != null && selected == match.CoreCombatant;

			perkOffers.Clear();
			TowerDefenseDollLabel doll = match.FindDoll(selected);
			if (doll != null && doll.Progress.PendingChoices > 0)
				TowerDefenseBuildingProgress.Offer(doll.BuildingId, doll.Progress.Level, doll.IsHarvester, perkOffers);

			coreCards.Clear();
			if (isCore)
				match.OfferCoreCards(coreCards);

			hud.ShowSelection(
				match.DescribeUnit(selected),
				canResearch: isCore,
				researchLevel: match.LabCount,
				researchCost: match.ResearchCost,
				researchUsesEssence: match.ResearchUsesEssence,
				perkOffers,
				coreCards);

			// 연구 길 — 값을 치르기 전에 무엇을 얻는지 보여준다(표는 규칙층이 준 것 그대로).
			if (isCore)
			{
				match.DescribeUnlockPath(unlockPath);
				hud.ShowUnlockPath(unlockPath, match.LabCount);
			}
		}

		private readonly System.Collections.Generic.List<TowerDefenseUnlockEntry> unlockPath = new();

		/// <summary> 고른 건물의 레벨업 선택 — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void ChoosePerk(TowerDefenseBuildingPerk perk)
		{
			placement.SuppressNextClick();
			match.ChooseBuildingPerk(placement.SelectedBuilding, perk);
		}

		/// <summary> 난이도 한 단계 — *다음 판*부터 걸린다(시작 조건이라 도는 판을 바꾸지 않는다). </summary>
		private void CycleDifficulty()
		{
			placement.SuppressNextClick();
			match.Difficulty = TowerDefenseDifficulty.Next(match.Difficulty);
			Debug.Log($"{nameof(TowerDefenseModeController)}: 난이도 → {TowerDefenseDifficulty.NameOf(match.Difficulty)} (다음 판부터)");
		}

		/// <summary> UI 배율 한 단계 — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void CycleUiScale()
		{
			placement.SuppressNextClick();
			hud?.CycleUiScale();
		}

		/// <summary> 코어에서 연구 — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void DoResearch()
		{
			placement.SuppressNextClick();
			match.TryResearch();
		}

		/// <summary> 전체 사거리 표시 토글(디버그) — 그 클릭이 설치로 새지 않게 한 번 삼킨다. </summary>
		private void ToggleAllRanges()
		{
			placement.SuppressNextClick();
			match.ToggleAllRanges();
		}

		/// <summary> 핫바 클릭 — 숫자키와 같은 경로. 그 클릭이 설치로도 새지 않게 한 번 삼킨다. </summary>
		private void SelectSlotFromUi(int slot)
		{
			placement.SuppressNextClick();
			placement.SelectSlot(slot);
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

			if (isTowerDefense == wasTowerDefense)
				return; // 전이 아님 — 전략 스왑/매치 토글 생략(셀렉터 레이스·중복 방지).

			wasTowerDefense = isTowerDefense;

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
					&& resumeOwner.TowerDefenseResume != null
					&& resumeOwner.TowerDefenseResume.IsResumable)
				{
					match.RestoreSave(resumeOwner.TowerDefenseResume);
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
					view.ExitRequested += () => GameModeManager.Instance.SetMode(GameMode.Default);
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
					view.ResearchRequested += DoResearch;
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
					saveOwner.TowerDefenseResume = match.CaptureSave();
					saveOwner.SaveManager.SaveData();
				}

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
					hud.ResearchRequested -= DoResearch;
					hud.UiScaleCycleRequested -= CycleUiScale;
					hud.BuildingPerkChosen -= ChoosePerk;
					hud.CoreCardChosen -= ChooseCoreCard;
					hud.DifficultyCycleRequested -= CycleDifficulty;
				}
				placement.Deactivate();
				hud?.Hide();
				inputManager.SetInputStrategy(new InputStrategyWorld());
			}
		}
	}
}
