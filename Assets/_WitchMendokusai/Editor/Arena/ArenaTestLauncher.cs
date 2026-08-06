using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 v1 라이브 매치 dev 런처 — World 를 Play 로 부팅한 뒤 메뉴 실행하면 부팅된 컨텍스트
	/// (ObjectPoolManager/TimeManager) 안에 아레나(데이터=ArenaMapSO)를 z=1000 오프셋으로 생성·스폰·관전.
	/// Arena.unity 씬 불요(맵은 데이터 빌드). 콘텐츠 PlayMode 검증/튜닝용 수동 트리거.
	///
	/// 진입점 3:
	/// - (v1) = 전술 에디터 UI 거쳐 시작.
	/// - (Headless) = UI 게이트 우회 즉시 시작(behavior-verify 자동화).
	/// - (Arm Auto-Verify) = 다음 Play 진입 시 World 부팅 감지→자동 매치→종결 로그→Play 자동 종료.
	///   WM heavy-boot 중 MCP HTTP 브릿지 wedge 라 Play 중 메뉴/MCP 호출 불가 → edit 모드(브릿지 생존)서
	///   플래그만 박고 Play 누르면 에디터 내부 핸들러가 전부 자동 = Claude 가 사용자 클릭 없이 Editor.log 만으로
	///   패트롤/전진 behavior-verify(`[Arena-Verify]` 라인). 플래그 = 진입 즉시 1회성 소거(정상 play 비파괴).
	/// </summary>
	public static class ArenaTestLauncher
	{
		// v1 콘텐츠 = 인형 3v3(알리사/서전크로스/티메토 vs 혼합). 슬라임 config(ArenaMatchConfig.asset)는 회귀 베이스라인으로 보존.
		private const string CONFIG_PATH = "Assets/_WitchMendokusai/Domain/Arena/Match/ArenaMatchConfig_Dolls.asset";
		private const float ARENA_OFFSET_Z = 1000f;
		private const string AUTOVERIFY_KEY = "WM_ARENA_AUTOVERIFY"; // EditorPrefs 1회성 플래그.
		private const string AUTOVERIFY_COUNT_KEY = "WM_ARENA_AUTOVERIFY_COUNT"; // 연속 매치 수(재매치 검증 = 2).
		private const int AUTOVERIFY_READY_TIMEOUT_FRAMES = 6000; // World 부팅(BootObserver.WorldReady) 대기 상한. 초과 = 포기+경고.
		private const int AUTOVERIFY_SETTLE_FRAMES = 30; // WorldReady 도달 후 정착 대기(씬/매니저 안정).

		private static int autoVerifyWaitFrames;
		private static int autoVerifySettleFrames;
		private static int autoVerifyMatchesLeft; // 남은 연속 매치 수(체이닝).

		// TASK-WM-165 item9 — 모드 검증 상태기 (마도서→투기장 경로 = SetMode(Arena)→ArenaModeController).
		private const string MODEVERIFY_KEY = "WM_ARENA_MODE_VERIFY";
		private const int MODEVERIFY_ENTER_FRAMES = 120; // SetMode(Arena) 후 매치 스폰/틱/카메라 블렌드 대기.
		private const int MODEVERIFY_EXIT_FRAMES = 40;   // SetMode(Default) 후 복귀 대기.
		private static int modeVerifyPhase;
		private static int modeVerifyFrames;

		[InitializeOnLoadMethod]
		private static void HookPlayModeStateChanged()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		[MenuItem("WM/Arena/Begin Test Match (v1)")]
		public static void BeginTestMatch()
		{
			if (TryPrepareMatch(out ArenaMatch match, out Transform root, out ArenaMatchConfig config) == false)
				return;

			// 프리-매치 전술 에디터 — 로스터 전술을 행 리스트로 편집 후 [매치 시작]. UIRoot 없으면 바로 시작.
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) && uiRoot.ScreenLayer != null)
			{
				List<TacticEditorView.Entry> entries = new List<TacticEditorView.Entry>();
				foreach (ArenaMatchConfig.ArenaUnitEntry rosterEntry in config.Roster)
				{
					if (rosterEntry.UnitData == null || rosterEntry.Tactic == null)
						continue;
					entries.Add(new TacticEditorView.Entry
					{
						Label = rosterEntry.UnitData.Name + " (팀" + rosterEntry.TeamId + ")",
						Authoring = new RowListAuthoring(rosterEntry.Tactic),
					});
				}
				new TacticEditorView(uiRoot.ScreenLayer, entries, () => match.Begin(config, root));
				Debug.Log("ArenaTestLauncher: 전술 에디터 열림 — 행 편집 후 [매치 시작] 클릭. z=" + ARENA_OFFSET_Z + " 관전.");
			}
			else
			{
				Debug.LogWarning("ArenaTestLauncher: UIRoot 없음 — 에디터 생략, 바로 매치 시작.");
				match.Begin(config, root);
			}
		}

		[MenuItem("WM/Arena/Begin Test Match (Headless, no UI)")]
		public static void BeginTestMatchHeadless()
		{
			StartHeadlessMatch();
		}

		[MenuItem("WM/Arena/Arm Auto-Verify (next Play)")]
		public static void ArmAutoVerify()
		{
			EditorPrefs.SetBool(AUTOVERIFY_KEY, true);
			EditorPrefs.SetInt(AUTOVERIFY_COUNT_KEY, 1);
			Debug.Log("[Arena-Verify] ARMED (x1) — 다음 Play 진입 시 자동 매치+종결+Play 종료. 이제 Play(▶) 를 누르세요.");
		}

		[MenuItem("WM/Arena/Arm Auto-Verify Rematch (x2, next Play)")]
		public static void ArmAutoVerifyRematch()
		{
			EditorPrefs.SetBool(AUTOVERIFY_KEY, true);
			EditorPrefs.SetInt(AUTOVERIFY_COUNT_KEY, 2);
			Debug.Log("[Arena-Verify] ARMED (x2 rematch) — 다음 Play 진입 시 자동 매치 2연속+Play 종료(재매치 검증). 이제 Play(▶) 를 누르세요.");
		}

		[MenuItem("WM/Arena/Arm Mode-Verify (마도서→투기장, next Play)")]
		public static void ArmModeVerify()
		{
			EditorPrefs.SetBool(MODEVERIFY_KEY, true);
			Debug.Log("[Arena-Mode-Verify] ARMED — 다음 Play 진입 시 자동 SetMode(Arena)→카메라/매치/입력 확인→SetMode(Default)→복귀+Play 종료. 이제 Play(▶) 를 누르세요.");
		}

		/// <summary> 헤드리스 매치 시작 — UI 게이트 우회 즉시 Begin. 실패 시 null. </summary>
		private static ArenaMatch StartHeadlessMatch()
		{
			if (TryPrepareMatch(out ArenaMatch match, out Transform root, out ArenaMatchConfig config) == false)
				return null;

			match.Begin(config, root);

			// ★ Begin 은 로스터·맵 검증(팀 수 / 팀당 스폰 / 스폰 겹침)에 걸리면 **LogError 만 남기고
			//   조용히 돌아온다.** 그러면 코루틴도 안 돌고 MatchEnded 도 안 와서, 자동 검증은
			//   판정 한 줄 없이 Play 에 매달린 채 끝난다(타임아웃은 부팅 대기 구간에만 있다).
			//   0클릭 검증은 「왜 안 됐는지」를 스스로 말해야 쓸모가 있다.
			if (match.IsRunning == false)
			{
				Debug.LogError("[Arena-Verify] MATCH-NOT-STARTED — Begin 이 거절했다. "
					+ "바로 위 ArenaMatch LogError 를 볼 것(로스터 TeamId 범위 / 팀당 유닛 수 / 스폰 겹침 / config 미할당).");
				return null;
			}

			Debug.Log("[Arena-Verify] HEADLESS — UI 게이트 우회, 즉시 매치 시작. z=" + ARENA_OFFSET_Z);
			return match;
		}

		/// <summary> 공통 셋업 — config 로드 + 아레나 루트(z 오프셋) + 관전 카메라 + ArenaMatch 컴포넌트 생성. 실패 시 false. </summary>
		private static bool TryPrepareMatch(out ArenaMatch match, out Transform root, out ArenaMatchConfig config)
		{
			match = null;
			root = null;
			config = null;

			if (Application.isPlaying == false)
			{
				Debug.LogWarning("ArenaTestLauncher: 먼저 World 를 Play 로 부팅한 뒤 실행하세요 (ObjectPoolManager/TimeManager 필요).");
				return false;
			}

			config = AssetDatabase.LoadAssetAtPath<ArenaMatchConfig>(CONFIG_PATH);
			if (config == null)
			{
				Debug.LogError("ArenaTestLauncher: ArenaMatchConfig 없음 — " + CONFIG_PATH);
				return false;
			}

			// 이전 매치 잔재 정리(같은 Play 세션 재매치 누수 방지). ★ Dispose() 를 *동기* 호출 —
			// Object.Destroy 는 end-of-frame 이라 OnDestroy→Dispose 가 다음 매치 스폰 *후* 실행됨.
			// 다음 매치가 같은 풀 인스턴스를 재사용하면, 늦은 Dispose 의 StopDriving(RemoveCallback+runner=null)이
			// 방금 등록한 OnTick 콜백/runner 를 clobber → 재사용 유닛이 안 tick(미전투). 동기 Dispose 로 스폰 전 완결.
			foreach (ArenaMatch existing in Object.FindObjectsByType<ArenaMatch>())
			{
				existing.Dispose();
				Object.Destroy(existing.gameObject);
			}
			GameObject priorRoot = GameObject.Find("ArenaMatchRoot");
			if (priorRoot != null)
				Object.Destroy(priorRoot);
			GameObject priorCamera = GameObject.Find("ArenaSpectatorCamera");
			if (priorCamera != null)
				Object.Destroy(priorCamera);

			GameObject rootGameObject = new GameObject("ArenaMatchRoot");
			rootGameObject.transform.position = new Vector3(0f, 0f, ARENA_OFFSET_Z);
			root = rootGameObject.transform;

			GameObject cameraGameObject = new GameObject("ArenaSpectatorCamera");
			Camera spectatorCamera = cameraGameObject.AddComponent<Camera>();
			cameraGameObject.transform.position = new Vector3(0f, 26f, ARENA_OFFSET_Z - 30f);
			cameraGameObject.transform.rotation = Quaternion.Euler(41f, 0f, 0f);
			spectatorCamera.fieldOfView = 60f;
			spectatorCamera.depth = 100f;

			GameObject matchGameObject = new GameObject(nameof(ArenaMatch));
			match = matchGameObject.AddComponent<ArenaMatch>();
			match.MatchEnded += winnerTeamId => Debug.Log("[ArenaTestLauncher] 매치 종료 — 승리 팀 = " + winnerTeamId + " (-1 = 무승부)");

			return true;
		}

		// --- Auto-Verify: edit 모드서 ARM → 다음 Play 진입 시 자동 구동 (MCP wedge 무관, 에디터 내부 핸들러) ---

		private static void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.EnteredPlayMode)
				return;

			// TASK-WM-165 item9 — 모드 검증(마도서→투기장 경로). SetMode(Arena)→ArenaModeController→카메라/매치/입력.
			if (EditorPrefs.GetBool(MODEVERIFY_KEY, false))
			{
				EditorPrefs.SetBool(MODEVERIFY_KEY, false);
				autoVerifyWaitFrames = 0;
				autoVerifySettleFrames = 0;
				EditorApplication.update -= ModeVerifyWaitForBoot;
				EditorApplication.update += ModeVerifyWaitForBoot;
				Debug.Log("[Arena-Mode-Verify] AUTO-START armed — World 부팅 대기 중...");
				return;
			}

			if (EditorPrefs.GetBool(AUTOVERIFY_KEY, false) == false)
				return;

			// 1회성 — 진입 즉시 소거(이후 정상 play 는 절대 자동 매치 X).
			EditorPrefs.SetBool(AUTOVERIFY_KEY, false);
			autoVerifyMatchesLeft = Mathf.Max(1, EditorPrefs.GetInt(AUTOVERIFY_COUNT_KEY, 1));
			autoVerifyWaitFrames = 0;
			autoVerifySettleFrames = 0;
			EditorApplication.update -= AutoVerifyWaitForBoot;
			EditorApplication.update += AutoVerifyWaitForBoot;
			Debug.Log($"[Arena-Verify] AUTO-START armed (x{autoVerifyMatchesLeft}) — World 부팅 대기 중...");
		}

		// World 부팅(매니저 준비) 폴 → 준비되면 헤드리스 매치 시작 + 종결 시 Play 자동 종료.
		private static void AutoVerifyWaitForBoot()
		{
			if (Application.isPlaying == false)
			{
				EditorApplication.update -= AutoVerifyWaitForBoot;
				return;
			}

			autoVerifyWaitFrames++;
			if (autoVerifyWaitFrames > AUTOVERIFY_READY_TIMEOUT_FRAMES)
			{
				EditorApplication.update -= AutoVerifyWaitForBoot;
				Debug.LogWarning("[Arena-Verify] AUTO-START 포기 — 매니저(ObjectPoolManager/TimeManager) 미준비(타임아웃). World 씬 맞나 확인.");
				return;
			}

			// World 조립 완료(WorldReady) 까지 대기 — Boot/Lobby/Loading 씬 전환 중 시작하면 씬 언로드가 아레나 GO 파괴.
			// BootObserver.ReachedWorld = WM 부팅 완료 센티넬(BootSmokeSentinel 와 동일 훅).
			if (BootObserver.ReachedWorld == false)
				return;

			// WorldReady 후 정착 — 매니저/씬 안정 몇 프레임.
			autoVerifySettleFrames++;
			if (autoVerifySettleFrames < AUTOVERIFY_SETTLE_FRAMES)
				return;

			if (TimeManager.Instance == null || ObjectPoolManager.Instance == null)
				return; // 안전 가드 — 보통 WorldReady 면 준비됨.

			EditorApplication.update -= AutoVerifyWaitForBoot;
			StartNextAutoVerifyMatch();
		}

		// 연속 매치 체이닝 — 한 매치 종결 시 남은 카운트 있으면 다음 매치(재매치 검증), 없으면 Play 자동 종료.
		private static void StartNextAutoVerifyMatch()
		{
			ArenaMatch match = StartHeadlessMatch();
			if (match == null)
			{
				if (Application.isPlaying)
					EditorApplication.isPlaying = false;
				return;
			}

			match.MatchEnded += _ =>
			{
				autoVerifyMatchesLeft--;
				if (autoVerifyMatchesLeft > 0)
				{
					// 다음 매치 — 한 프레임 양보(이전 매치 Dispose/teardown 정착 후).
					EditorApplication.delayCall += () =>
					{
						if (Application.isPlaying)
							StartNextAutoVerifyMatch();
					};
					return;
				}

				// 마지막 매치 종결 → Play 자동 종료 → 도메인 리로드 → MCP 브릿지 회복.
				EditorApplication.delayCall += () =>
				{
					if (Application.isPlaying)
						EditorApplication.isPlaying = false;
					Debug.Log("[Arena-Verify] AUTO-END — Play 자동 종료. Editor.log 의 [Arena-Verify] MATCH-END 로 판정.");
				};
			};
		}

		// --- Mode-Verify: 마도서→투기장 경로(SetMode(Arena)→ArenaModeController) 자율 검증. Editor.log ground-truth. ---

		private static void ModeVerifyWaitForBoot()
		{
			if (Application.isPlaying == false)
			{
				EditorApplication.update -= ModeVerifyWaitForBoot;
				return;
			}

			autoVerifyWaitFrames++;
			if (autoVerifyWaitFrames > AUTOVERIFY_READY_TIMEOUT_FRAMES)
			{
				EditorApplication.update -= ModeVerifyWaitForBoot;
				Debug.LogWarning("[Arena-Mode-Verify] 포기 — World 미준비(타임아웃). World 씬 맞나 확인.");
				return;
			}

			if (BootObserver.ReachedWorld == false)
				return;

			autoVerifySettleFrames++;
			if (autoVerifySettleFrames < AUTOVERIFY_SETTLE_FRAMES)
				return;

			if (GameModeManager.Instance == null || CameraManager.Instance == null)
				return;

			EditorApplication.update -= ModeVerifyWaitForBoot;
			StartModeVerify();
		}

		private static void StartModeVerify()
		{
			bool controllerExists = ArenaModeController.TryGetExistingInstance(out _);
			Debug.Log($"[Arena-Mode-Verify] BOOT-OK controllerExists={controllerExists} mode={GameModeManager.Instance.CurrentMode}");

			GameModeManager.Instance.SetMode(GameMode.Arena);
			Debug.Log($"[Arena-Mode-Verify] ENTER-CALLED mode={GameModeManager.Instance.CurrentMode}");

			modeVerifyPhase = 0;
			modeVerifyFrames = 0;
			EditorApplication.update -= ModeVerifyTick;
			EditorApplication.update += ModeVerifyTick;
		}

		private static void ModeVerifyTick()
		{
			if (Application.isPlaying == false)
			{
				EditorApplication.update -= ModeVerifyTick;
				return;
			}

			modeVerifyFrames++;

			if (modeVerifyPhase == 0)
			{
				if (modeVerifyFrames < MODEVERIFY_ENTER_FRAMES)
					return;

				bool spectatorActive = UnityEngine.GameObject.Find("ArenaSpectatorCamera") != null;
				bool isSpectating = GameConditionBridge.Get(GameConditionType.IsSpectating);
				// 매치 구동 여부를 *있는 신호*로 찍는다 — 예전엔 「별도 MATCH-START 로그가 있나」를
				// 읽는 쪽이 알아채야 했다(없는 줄을 알아채는 건 사람이 제일 못 하는 일).
				bool matchRunning = ArenaModeController.TryGetExistingInstance(out ArenaModeController arenaController)
					&& arenaController.IsMatchRunning;
				Debug.Log($"[Arena-Mode-Verify] ENTER-STATE mode={GameModeManager.Instance.CurrentMode} spectatorCamActive={spectatorActive} isSpectating={isSpectating} matchRunning={matchRunning} (셋 다 True = 관전화면+입력잠금+매치 구동)");

				GameModeManager.Instance.SetMode(GameMode.Default);
				modeVerifyPhase = 1;
				modeVerifyFrames = 0;
				return;
			}

			if (modeVerifyFrames < MODEVERIFY_EXIT_FRAMES)
				return;

			bool spectatorActiveAfter = UnityEngine.GameObject.Find("ArenaSpectatorCamera") != null;
			bool isSpectatingAfter = GameConditionBridge.Get(GameConditionType.IsSpectating);
			Debug.Log($"[Arena-Mode-Verify] EXIT-STATE mode={GameModeManager.Instance.CurrentMode} spectatorCamActive={spectatorActiveAfter} isSpectating={isSpectatingAfter} (mode=Default+spectating=false+spectatorCamActive=false = 복귀 OK)");

			EditorApplication.update -= ModeVerifyTick;
			Debug.Log("[Arena-Mode-Verify] DONE — Play 자동 종료. Editor.log 의 ENTER-STATE/EXIT-STATE 로 판정.");
			EditorApplication.delayCall += () =>
			{
				if (Application.isPlaying)
					EditorApplication.isPlaying = false;
			};
		}

	}
}
