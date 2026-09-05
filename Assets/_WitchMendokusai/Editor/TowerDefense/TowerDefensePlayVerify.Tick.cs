using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	// TowerDefensePlayVerify 의 진행 상태 기계 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			// ★ 검사는 *같은 땅*에서 돌아야 두 실행을 견줄 수 있다. 씨앗이 매번 다르면 암반 배치가 통째로
			//   달라져, 좋아진 것이 내 수정 덕인지 판이 쉬웠던 덕인지 영영 못 가른다(실측: 같은 코드로
			//   굳음 경고가 99~421 을 오갔고, 그 흔들림을 수정 효과로 두 번 잘못 읽었다).
			// ★ 매 틱 다시 건다 — 한 실행 안에서 판이 여러 번 새로 태어나는데(재시작·무방비 판),
			//   한 번만 걸었더니 첫 판만 고정되고 나머지는 도로 무작위였다(실측으로 확인).
			//   지정값은 판이 태어날 때 한 번 쓰이고 지워지므로, 매 틱 거는 것이 곧 「매 판 고정」이다.
			//   사람이 노는 판은 그대로 매번 새로 생성된다 — 이 코드는 검사에만 있다.
			if (match != null)
				match.SetNextMatchSeed(VERIFY_MAP_SEED);

			// 안전망 — 무슨 일이 있어도 공유 에디터를 Play 에 물리지 않는다.
			if (now - playStart > HARD_TIMEOUT)
			{
				// 타임아웃이 그냥 "행"으로만 끝나면 몇 분짜리 실행이 통째로 버려진다 — 죽기 전에 아는 것을 전부 말한다.
				Debug.LogError(TAG + " TIMEOUT — 단계=" + step + " 에서 행. Play 강제 종료."
					+ " match=" + (match != null)
					+ (match != null
						? " phase=" + match.Phase + " wave=" + match.WaveIndex + " outcome=" + match.Outcome
							+ " resource=" + match.Resource
							+ " coreAlive=" + (match.CoreCombatant != null && match.CoreCombatant.IsAlive)
						: string.Empty)
					+ " endedEvent=" + matchEndedSeen
					+ " observed=" + (observeStart > 0 ? (now - observeStart).ToString("F1") : "n/a"));
				Finish();
				return;
			}

			if (relayProbeAt > 0.0 && now >= relayProbeAt)
			{
				relayProbeAt = 0.0;
				CheckRelayChain();
			}

			ArmAdaptationProbe();
			PollAdaptation(now);

			ArmNoiseProbe();
			// ★ 알림 칸은 넷뿐이고 같은 순간에 여럿 터지면 *먼저 난 것*이 밀려난다. 2초 뒤에 물으면
			//   이미 밀려난 뒤라 늘 「없음」이다 — 그래서 뜨는 순간을 매 틱 지켜본다(시험으로 못 박은 함정).
			if (noiseSustainAt > 0.0 && now >= noiseSustainAt)
			{
				noiseSustainAt = 0.0;
				CheckNoiseSustained();
			}

			TickNoiseRamp();

			if (noiseCheckAt > 0.0)
			{
				WatchNoiseAlert();
				if (now >= noiseCheckAt)
				{
					noiseCheckAt = 0.0;
					CheckNoiseWake();
				}
			}

			ArmBreachProbe(now);
			if (breachCheckAt > 0.0 && now >= breachCheckAt)
			{
				breachCheckAt = 0.0;
				CheckBreachPull();
			}

			if (lairClearCheckAt > 0.0 && now >= lairClearCheckAt)
			{
				lairClearCheckAt = 0.0;
				CheckLairClearReward();
			}

			if (lairDriftCheckAt > 0.0 && now >= lairDriftCheckAt)
			{
				lairDriftCheckAt = 0.0;
				CheckLairDrift();
			}

			if (pressureCheckAt > 0.0 && now >= pressureCheckAt)
			{
				pressureCheckAt = 0.0;
				CheckPressureNotice();
			}

			if (markCheckAt > 0.0 && now >= markCheckAt)
			{
				markCheckAt = 0.0;
				CountOnScreenMarks();
			}

			// ★ 신호·서식지는 **판이 도는 중에** 재야 한다. 배치 직후는 아직 첫 틱도 안 돈 시점이라
			//   전기 계산이 시작조차 안 했고, 서식지 스폰 코루틴도 절반만 끝나 있다 — 거기서 재면
			//   멀쩡한 것을 「0 이다」라고 잡는다(실측으로 한 번 겪었다: 버틴시간 0초에 전부 0).
			if (signalChecked == false && match != null && match.SurvivedSeconds >= 5)
			{
				signalChecked = true;
				VerifySignalField();
				VerifyLairsAndInvasion();
				VerifyOnScreenMarks();
			}

			switch (step)
			{
				case Step.WaitWorld:
					{
						bool sceneOk = SceneIsWorld();
						bool bootOk = BootObserver.ReachedWorld;
						bool modeOk = GameModeManager.TryGetExistingInstance(out GameModeManager _);
						bool ctrlOk = TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller);

						// 게이트별 주기 로그 — "행"이 부팅 지연인지 컨트롤러 미생성인지 로그만으로 갈린다.
						if (now - lastGateLog >= GATE_LOG_INTERVAL)
						{
							lastGateLog = now;
							Debug.Log(TAG + " GATE t=" + (now - playStart).ToString("F0")
								+ " scene=" + SceneManager.GetActiveScene().name
								+ " sceneIsWorld=" + sceneOk
								+ " bootPhase=" + BootObserver.Current
								+ " gameModeMgr=" + modeOk
								+ " tdController=" + ctrlOk);
						}

						// 로비 자동 통과 — AppSettings.AutoStart 기본값이 false 라(헤드리스/dev 옵션에서만 true)
						// 부팅이 DataReady 에서 사람 클릭을 기다린다. 사용자의 저장 설정을 건드리지 않고
						// 하네스가 "시작" 을 대신 눌러 World 로 넘긴다(멱등 — startClicked 1회).
						if (bootOk == false && startClicked == false
							&& BootObserver.Current == BootPhase.DataReady
							&& LobbyManager.Instance != null)
						{
							startClicked = true;
							Debug.Log(TAG + " LOBBY-AUTOSTART — AutoStart=false 라 하네스가 StartGame 대행");
							LobbyManager.Instance.StartGame();
							return;
						}

						if (sceneOk == false || bootOk == false || modeOk == false || ctrlOk == false)
							return;

						match = controller.GetComponent<TowerDefenseMatch>();
						Debug.Log(TAG + " BOOT-OK controller=True match=" + (match != null));
						readyAt = now;
						step = Step.Settle;
						return;
					}

				case Step.Settle:
					if (now - readyAt < SETTLE_SECONDS)
						return;
					step = Step.EnterMode;
					return;

				case Step.EnterMode:
					if (GameModeManager.TryGetExistingInstance(out GameModeManager modeManager) == false)
						return;
					VerifyTimetoHub();
					modeManager.SetMode(GameMode.TowerDefense);
					match.MatchEnded += OnMatchEnded;
					Debug.Log(TAG + " ENTER-MODE mode=" + modeManager.CurrentMode);
					DumpCameras("진입 직후");
					step = Step.WaitMatch;
					return;

				case Step.WaitMatch:
					// 코어 생성 = TowerDefenseCore 존재 = Resource 가 시작자원으로 채워짐.
					if (match == null || match.Resource <= 0)
						return;
					Debug.Log(TAG + " MATCH-READY resource=" + match.Resource + " phase=" + match.Phase
						+ " conclusionOnly=" + conclusionOnly);
					// 결말만 모드 = 아무것도 짓지 않은 채 그대로 관측 — 이미 무방비 상태라 재시작조차 필요 없다.
					if (conclusionOnly)
					{
						restartAt = now;
						step = Step.ObserveConclusion;
						return;
					}
					step = Step.Place;
					return;

				case Step.Place:
					DoPlacements();
					// 2차 덤프 — "진입 시엔 켜졌는데 이후 덮인다"를 잡으려면 시간 경과 후 한 번 더 봐야 한다.
					DumpCameras("배치 후");
					restartAt = now;
					step = Step.PlaceDump;
					return;

				// 배치 스폰은 코루틴(1프레임 양보 후 Init/등록)이라 **같은 틱에 덤프하면 아무것도 안 보인다**
				// (첫 시도에서 "수비 유닛 1기"만 찍혀 진단이 통째 유실됐다). 등록이 끝날 시간을 준다.
				case Step.PlaceDump:
					if (now - restartAt < 1.5)
						return;
					DumpPlacedUnits("최초 배치");
					VerifyUiPointerGuard();
					VerifyHudLayout("평상시");
					// ★ 평상시엔 안 뜨는 것(선택 패널)이 *뜬 상태*로도 재야 한다 — 안 뜬 것은 겹칠 수도 없어서
					//   「겹침 0」이 「띄워본 적이 없다」를 숨긴다.
					// ★ 코어 카드는 레벨이 올라야 뜬다 — 안 띄우면 「카드가 겹치나」를 영영 못 잰다.
					if (match != null)
						match.GrantCoreExperienceForVerification(CORE_XP_FOR_CARDS);
					VerifyBuildingPerk();
					SelectPlacedBuildingForLayout();
					// ★ 예산은 유한하고 확인할 항목은 여럿이다 — 순서가 곧 검증 가능 여부다.
					//   승급은 *이미 서 있는 포탑*이 필요하므로 판매보다 먼저, 비싼 연구 인형은 맨 뒤.
					// 예산 순 — 채집(60)이 필요한 정수 확인을 먼저, 비싼 연구를 맨 뒤로.
					VerifyEssence();
					VerifyUpgrade();
					VerifyEssenceShortageTalks();
					VerifySell();
					VerifyTrap();
					VerifyWall();
					VerifyResearch();
					VerifyWaveEvents();
					VerifySupply();
					// 씨앗 공유는 새 판에서만 확인된다 — 재시작이 그 새 판이므로 여기서 걸어둔다.
					ArmSeedShareCheck();
					// ★ 전체 실행도 이 길로 보낸다. 예전엔 「배치만」 변형만 들렀는데, 그 변형을 아무도
					//   안 돌리는 바람에 *선택 패널 겹침 · 툴팁 겹침 · 코어 카드 · 이어하기* 검사가
					//   로그 전체에서 0회였다(오늘 하루치를 다 뒤져도 한 줄도 없다). 도는 길에 있어야 검사다.
					// ★ 파도만 실행은 여기서 *끝내는* 게 아니라 파도 관측으로 **건너뛴다**.
					//   앞 판에서 여기서 끝냈더니 100초에 죽었지만 파도 표본이 한 줄도 없었다 —
					//   이 지점은 아직 배치 직후라 파도를 본 적이 없다. 화면·성능·굳음은 전부
					//   그 뒤(방어 관찰 → 무방비 결말)에서 나온다. 건너뛸 것은 화면 배치·이어하기·재시작이다.
					if (wavesOnly)
					{
						Debug.Log(TAG + " WAVES-ONLY 파도 관측으로 건너뛴다(화면배치·이어하기·재시작 생략)");
						defendedStart = now;
						defendedLastResource = match != null ? match.Resource : -1;
						killIncomeEvents = 0;
						step = Step.ObserveDefended;
						return;
					}

					step = Step.SelectedLayout;
					selectedLayoutAt = now;
					return;

				// ★ 패널은 *다음 배치 패스*에 열린다 — 클릭한 그 틱에 재면 「아직 안 뜬 것」을 재고
				//   「겹침 0」이라 적는다(거짓 통과). 한 틱 기다렸다 잰다.
				case Step.SelectedLayout:
					// ★ 서식지 이동 측정이 아직 안 끝났으면 모드를 나가지 않는다 — 나가면 판이 새로 태어나
					//   깨운 서식지가 통째로 사라져 그 측정이 영영 성립하지 않는다.
					// ★ 무엇을 기다리는지 말한다. 예전엔 조용히 머물다 7분 뒤 「행」으로만 죽어서,
					//   어느 항목이 안 끝났는지 알 길이 없었다(실측에서 이 단계가 통째로 막혔다).
					if (lairDriftCheckAt > 0.0 || lairClearCheckAt > 0.0 || pressureCheckAt > 0.0)
					{
						if (now - lastGateLog > 5.0)
						{
							lastGateLog = now;
							Debug.Log($"{TAG} SelectedLayout 대기 — 서식지이동 {lairDriftCheckAt > 0.0}"
								+ $" · 소탕보상 {lairClearCheckAt > 0.0} · 강도 {pressureCheckAt > 0.0}"
								+ $" · 버틴시간 {(match != null ? match.SurvivedSeconds : -1f):F0}");
						}
						return;
					}

					// ★ 앞 단계에서 열어둔 성좌를 *여기서* 닫는다. 닫는 자리를 재시작 단계에 뒀더니,
					//   이 단계가 그 앞으로 끼어드는 순간 성좌가 열린 채로 남아 판이 멈추고,
					//   멈춘 판은 시계가 안 가니 아래 시계 게이트를 영영 못 넘어 7분 뒤 시간초과로 죽었다.
					//   덮는 것을 여는 쪽과 닫는 쪽이 다른 단계에 있으면 이런 교착이 생긴다.
					MeasureAndCloseResearchPanel();
					if (now - selectedLayoutAt < 0.3)
						return;
					// ★ 겹침은 한 번만 잰다 — 아래 시계 게이트가 이 단계를 여러 틱 돌리므로,
					//   안 막으면 같은 판정이 로그를 도배해 진짜 신호가 묻힌다.
					if (selectedLayoutChecked == false)
					{
						selectedLayoutChecked = true;
						VerifyHudLayout("건물 선택 중", mustBeUp: "SelectionPanel");
					ShowTooltipForLayout();
					VerifyHudLayout("툴팁 떠 있음");
					SelectCoreForLayout();
					VerifyCoreCards();
					}
					// ★ 시계가 0 일 때 나가면 「되감겼는지」를 가릴 수 없다(0 이나 1 이나 통과).
					//   눈금이 실제로 쌓인 뒤에 나가야 이어하기가 시계를 지키는지가 증명된다.
					if (match != null && match.SurvivedSeconds < RESUME_MIN_CLOCK)
					{
						if (now - lastGateLog > 5.0)
						{
							lastGateLog = now;
							Debug.Log($"{TAG} SelectedLayout 대기 — 시계가 아직 {match.SurvivedSeconds:F0}"
								+ $"/{RESUME_MIN_CLOCK} (판이 멈춰 있으면 시계도 안 간다)");
						}
						return;
					}
					CaptureResumeSnapshot();
					if (GameModeManager.TryGetExistingInstance(out GameModeManager exitManager))
						exitManager.SetMode(GameMode.Default);
					resumeAt = now;
					step = Step.ResumeEnter;
					return;

				// ★ 「잠깐 접어둔다」가 진짜인지 — 나갔다 들어와서 그 판이 그대로 있는지 본다.
				//   이게 없으면 저장은 *써지기만 하고 아무도 안 읽는* 상태로도 통과한다(실제로 그랬다).
				case Step.ResumeEnter:
					if (now - resumeAt < 1.5)
						return;
					if (GameModeManager.TryGetExistingInstance(out GameModeManager enterManager))
						enterManager.SetMode(GameMode.TowerDefense);
					resumeAt = now;
					step = Step.ResumeCheck;
					return;

				case Step.ResumeCheck:
					if (now - resumeAt < 4.0)
						return;
					// ★ 복원이 끝날 때까지 이 단계에 머문다 — 도는 중에 재면 중간값을 결함으로 잡는다.
					{
						TowerDefenseMatch restoring = Object.FindAnyObjectByType<TowerDefenseMatch>();
						if (restoring != null && restoring.RestoreInProgress)
							return;
					}
					// ★ 이 단계는 남은 검사를 기다리느라 여러 틱 머문다. 그때마다 다시 재면
					//   *살아 움직이는 판*(목숨이 줄고 시간이 흐르는)과 복원 직후 스냅샷을 비교하게 되어
					//   에러가 매 프레임 쏟아진다 — 실측에서 같은 에러 31줄이 찍혀 진짜 에러를 덮었다.
					//   복원 비교는 복원 직후 딱 한 번이다.
					if (resumeVerified == false)
					{
						resumeVerified = true;
						VerifyResume();
					}

					if (placeOnly)
					{
						// 아직 재기로 한 것이 남아 있으면 끝내지 않는다 — 끝내버리면 그 항목은 영영 안 재진다.
						if (lairDriftCheckAt > 0.0 || markCheckAt > 0.0 || lairClearCheckAt > 0.0 || pressureCheckAt > 0.0
							|| relayProbeAt > 0.0 || adaptationProbeAt > 0.0 || breachCheckAt > 0.0 || noiseCheckAt > 0.0 || noiseSustainAt > 0.0)
							return;

						Debug.Log(TAG + " PLACE-ONLY 배치 확인 끝 — 조기 종료");
						Finish();
						return;
					}

					// 전체 실행은 이어하기까지 본 뒤 원래 가던 재시작으로 잇는다.
					restartAt = now;
					step = Step.Restart;
					return;

				case Step.PlaceAfterRestartDump:
					if (now - restartAt < 1.5)
						return;
					DumpPlacedUnits("재시작 후 배치");
					// ★ 씨앗 공유는 *새 판*에서만 확인된다 — 이어하기는 저장이 씨앗을 정하므로.
					VerifySeedShare();
					defendedStart = now;
					defendedLastResource = match != null ? match.Resource : -1;
					killIncomeEvents = 0;
					step = Step.ObserveDefended;
					return;

				// ★ 방어를 세운 채 한 판을 실제로 지켜본다. 이 구간이 없으면 「마수를 잡았을 때 무슨 일이
				//   일어나는가」가 통째로 미검증으로 남는다(무방비 판은 아무도 안 죽으니 격파 보상이 안 보인다).
				case Step.ObserveDefended:
					ObserveDefended(now);
					return;

				// ★ 결말(패배)을 *빠르고 확실하게* 관측하기 위한 무방비 판.
				//   방어를 세워두면 여러 웨이브를 버텨 결말까지 수 분이 걸리고, 그동안 "게임이 끝나는가"는
				//   영영 미검증으로 남는다(실제로 그랬다). 아무것도 안 지으면 첫 웨이브가 코어를 깎아
				//   결말이 결정적으로 온다 — 조작이 아니라 *실제 게임 규칙 그대로*의 최단 경로다.
				// ★ 정수는 *늦게* 붙는다 — 발전 인형도 코루틴으로 서고 전기·보급 셈은 그 다음이라,
				//   배치 직후에 재면 늘 「전기가 안 닿음」이다(실측: 그때는 0, 1분 뒤에 물으니 1이었다).
				//   기다리는 시간을 늘려 맞추려다 순서만 깨뜨렸다 — 방어 관찰이 끝난 *여기서* 한 번 더 잰다.
				case Step.DisarmRestart:
					VerifyEssence("늦게");
					if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController disarmController) == false)
					{
						Debug.LogError(TAG + " DISARM-FAIL controller 없음");
						Finish();
						return;
					}
					Debug.Log(TAG + " DISARM-RESTART 무방비 판 시작 — 결말 도달 관측");
					disarmController.Restart();
					restartAt = now;
					step = Step.ObserveConclusion;
					return;

				case Step.ObserveConclusion:
					if (now - restartAt < 3.0)
						return;
					if (observeStart < 0.0 || observeStart < restartAt)
					{
						observeStart = now;
						lastSample = now;
						matchEndedSeen = false;
						if (match != null)
							match.RequestNextWave(); // 첫 웨이브는 불러야 온다 — 무방비 판도 마찬가지.
					}
					Observe(now);
					return;

				// ★ 재시작은 풀 재사용이 처음으로 *실제로* 일어나는 지점이다 — 최초 매치는 늘 새 인스턴스라
				//   초기화 누락이 드러나지 않는다. 사용자 실증("재시작하면 초기화가 덜 되고 위치도 이상")을
				//   재현하려면 하네스가 반드시 이 사이클을 밟아야 한다.
				case Step.Restart:
					// 앞 단계에서 열어둔 성좌 — *자리가 잡힌 지금* 재고 닫는다(재시작 전에).
					MeasureAndCloseResearchPanel();
					if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController restartController) == false)
					{
						Debug.LogError(TAG + " RESTART-FAIL controller 없음");
						Finish();
						return;
					}
					Debug.Log(TAG + " RESTART 요청 — 풀 재사용 경로 진입");
					restartController.Restart();
					restartAt = now;
					step = Step.RestartSettle;
					return;

				case Step.RestartSettle:
					// 재시작은 1프레임 양보 + 코어 스폰 코루틴을 거친다. 자원이 시작값으로 돌아오면 준비 완료.
					if (now - restartAt < 2.0)
						return;
					if (match == null || match.Resource <= 0)
					{
						if (now - restartAt > 15.0)
						{
							Debug.LogError(TAG + " RESTART-FAIL 재시작 후 매치가 안 살아남 resource=" + (match != null ? match.Resource : -1));
							Finish();
						}
						return;
					}
					Debug.Log(TAG + " RESTART-READY resource=" + match.Resource + " phase=" + match.Phase + " wave=" + match.WaveIndex);
					step = Step.PlaceAfterRestart;
					return;

				case Step.PlaceAfterRestart:
					DumpPlacedUnits("재시작 직후(배치 전)"); // 코어만 있어야 하고, 코어는 반드시 (0,0,0).
					DoPlacements();
					restartAt = now;
					step = Step.PlaceAfterRestartDump;
					return;

				case Step.Observe:
					Observe(now);
					return;

				case Step.VerifyConclusion:
					VerifyConclusion(now);
					return;

				case Step.RestartFromConclusion:
					VerifyRestartFromConclusion(now);
					return;
			}
		}
	}
}
