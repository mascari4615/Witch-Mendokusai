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
	// TowerDefensePlayVerify 의 관찰과 덤프 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static double defendedStart;
		private static int defendedLastResource;
		private static bool firstWaveCalled;
		private static int lastDumpedWave;
		private static double waveDumpAt;

		private const double STUCK_ASSAULT_SECONDS = 25.0;
		private static double assaultStart = -1.0;
		private static bool stuckDumped;
		// 집계가 마지막으로 *움직인* 시점을 잡기 위한 직전 값 — 실시간에는 이 정체가 곧 고착이다.
		private static int lastAliveEnemyCount = -1;

		/// <summary> 코어가 "살아있다"고 세는 적을 전부 찍는다 — 화면과 대조해 유령/고착을 가른다. </summary>
		/// <summary>
		/// 판이 고착됐나 — 「마릿수가 한참 그대로」 하나로 판정한다.
		///
		/// ★ 두 가지를 여기서 못 박는다:
		///   ① *판 시계*로 잰다 — 사람이 멈추거나 느리게 해두면 에디터 시계만 흐르고 판은 그대로다.
		///   ② *한 곳*에서만 판정한다 — 예전엔 관찰 구간마다 사본이 있었고, 그중 하나가 「교전 중이면
		///      30초」로 재고 있었다. 실시간에는 늘 교전 중이라 그 사본은 **매 판 무조건** 경고를 찍었다
		///      (실측: 게임은 멀쩡히 1.2초마다 마수를 내보내는데 「30초째 정체」라고 적혔다).
		///      거짓 경고는 진짜 고착을 묻는다.
		/// </summary>
		private static void CheckStall()
		{
			if (match == null)
				return;

			int aliveTracked = match.AliveEnemyCount;
			float matchClock = match.SurvivedSeconds;

			if (aliveTracked != lastAliveEnemyCount)
			{
				lastAliveEnemyCount = aliveTracked;
				assaultStart = matchClock;
				stuckDumped = false;
				return;
			}

			if (aliveTracked <= 0)
				return;

			if (assaultStart < 0)
			{
				assaultStart = matchClock;
				return;
			}

			if (matchClock - assaultStart > STUCK_ASSAULT_SECONDS && stuckDumped == false)
			{
				stuckDumped = true;
				DumpWaveEnemies(matchClock - assaultStart);
			}
		}

		private static void DumpWaveEnemies(double elapsed)
		{
			MatchCombatant core = match.CoreCombatant;
			Debug.LogWarning(TAG + " STUCK-ASSAULT 판 시계 " + elapsed.ToString("F1") + "s 동안 마릿수 그대로 — wave=" + match.WaveIndex
				+ " coreAliveCount=" + match.AliveEnemyCount + " tracked=" + match.WaveEnemies.Count
				+ " 판시계=" + match.SurvivedSeconds + "s timeScale=" + Time.timeScale.ToString("F1"));

			// ★ 교착의 두 갈래를 가른다: 적이 코어를 *때리고 있는데 안 죽는* 것인가(코어 체력이 줄고 있다),
			//   아니면 *아예 안 때리는* 것인가(체력 그대로 = 아무도 아무것도 안 함 = 영구 교착).
			Debug.Log(TAG + " STUCK-CORE alive=" + (core != null && core.IsAlive)
				+ " hp=" + (core != null ? core.Hp + "/" + core.HpMax : "n/a"));

			int defendersAlive = 0;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant == null || combatant.TeamId != 0 || combatant.IsAlive == false)
					continue;
				defendersAlive++;
				Debug.Log(TAG + " STUCK-DEFENDER id=" + combatant.CombatantId
					+ " hp=" + combatant.Hp + "/" + combatant.HpMax + " pos=" + combatant.Position);
			}
			Debug.Log(TAG + " STUCK-DEFENDERS aliveCount=" + defendersAlive + " (코어 포함)");

			for (int index = 0; index < match.WaveEnemies.Count; index++)
			{
				MatchCombatant enemy = match.WaveEnemies[index];
				if (enemy == null)
				{
					Debug.Log(TAG + " STUCK-ENEMY[" + index + "] null(파괴됨)");
					continue;
				}

				Debug.Log(TAG + " STUCK-ENEMY[" + index + "] alive=" + enemy.IsAlive
					+ " hp=" + enemy.Hp + "/" + enemy.HpMax
					+ " activeInHierarchy=" + enemy.gameObject.activeInHierarchy
					+ " pos=" + enemy.transform.position
					+ " driver=" + (enemy.GetComponent<TacticDriver>() != null));
			}
		}

		/// <summary>
		/// 배치된 수비 유닛의 **초기화 상태 전량** 덤프 — 재시작 후 풀에서 되뽑힌 개체가
		/// 지난 판의 흔적(색·크기·애니메이터·드라이버·체력)을 뒤집어쓰지 않았는지 좌표와 함께 본다.
		/// 사용자 실증: "재시작할 때 유닛 다시 설치하면 초기화가 덜 된 것 같고 배치 위치도 이상하다".
		/// </summary>
		private static void DumpPlacedUnits(string phase)
		{
			if (match == null)
				return;

			Transform stageRoot = FindStageRoot();
			int index = 0;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant == null || combatant.TeamId != 0)
					continue; // 수비측만(코어/포탑/채집).

				MatchCombatant arena = combatant as MatchCombatant;
				UnitObject unit = arena != null ? arena.UnitObject : null;
				if (unit == null)
					continue;

				Vector3 world = unit.transform.position.ToSim();
				Vector3 local = stageRoot != null ? stageRoot.InverseTransformPoint(world.ToUnity()).ToSim() : world;

				int animatorsEnabled = 0;
				foreach (Animator animator in unit.GetComponentsInChildren<Animator>(true))
				{
					if (animator.enabled)
						animatorsEnabled++;
				}

				int brainsEnabled = 0;
				foreach (UnitBrain brain in unit.GetComponents<UnitBrain>())
				{
					if (brain.enabled)
						brainsEnabled++;
				}

				SpriteRenderer sprite = unit.SpriteRenderer;
				Debug.Log(TAG + " UNIT[" + phase + "][" + index + "] id=" + combatant.CombatantId
					+ " hp=" + combatant.Hp + "/" + combatant.HpMax
					+ " local=" + local
					+ " scale=" + unit.transform.localScale.x.ToString("F2")
					+ " color=" + (sprite != null ? sprite.color.ToString() : "no-sprite")
					+ " sprite=" + (sprite != null && sprite.sprite != null ? sprite.sprite.name : "NULL")
					+ " animOn=" + animatorsEnabled
					+ " brainOn=" + brainsEnabled
					+ " driver=" + (unit.GetComponent<TacticDriver>() != null)
					+ " autoCast=" + (unit.SkillHandler != null ? unit.SkillHandler.AutoCastEnabled.ToString() : "no-handler")
					+ " active=" + unit.gameObject.activeInHierarchy);
				index++;
			}

			Debug.Log(TAG + " UNITS[" + phase + "] 수비 유닛 " + index + "기");
		}

		private static void Observe(double now)
		{
			if (match == null)
			{
				// Play 가 이미 끝났으면 매치 파괴가 아니라 *씬 통째 언로드* — 하네스 종료 사유이지 게임 결함이 아니다.
				// (둘을 구분 못 하면 환경 아티팩트를 코드 버그로 오진한다.)
				if (EditorApplication.isPlaying == false)
				{
					Debug.LogWarning(TAG + " OBSERVE-END Play 가 관찰 도중 종료됨(씬 언로드) — 관찰 "
						+ (now - observeStart).ToString("F1") + "s 시점. 게임 결함 아님, 관찰 조기 중단.");
					Finish();
					return;
				}

				// 진단 — "match 가 null" 만으론 원인 불명(컴포넌트 파괴 vs 모드 이탈 vs 싱글톤 중복 파괴).
				bool ctrlAlive = TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController ctrl);
				string mode = GameModeManager.TryGetExistingInstance(out GameModeManager gm) ? gm.CurrentMode.ToString() : "no-manager";
				int ctrlCount = Object.FindObjectsByType<TowerDefenseModeController>(FindObjectsInactive.Include).Length;
				int matchCount = Object.FindObjectsByType<TowerDefenseMatch>(FindObjectsInactive.Include).Length;
				Debug.LogError(TAG + " OBSERVE-FAIL match null — controllerInstance=" + ctrlAlive
					+ " controllersInScene=" + ctrlCount
					+ " matchesInScene=" + matchCount
					+ " currentMode=" + mode
					+ " ctrlGameObject=" + (ctrlAlive && ctrl != null ? ctrl.gameObject.name : "n/a"));
				Finish();
				return;
			}

			if (match.Phase != lastPhase || match.WaveIndex != lastWaveIndex)
			{
				Debug.Log(TAG + " STATE phase=" + match.Phase + " wave=" + match.WaveIndex
					+ " resource=" + match.Resource + " t=" + (now - observeStart).ToString("F1"));
				lastPhase = match.Phase;
				lastWaveIndex = match.WaveIndex;
			}

			if (match.Resource != lastResource)
			{
				if (lastResource >= 0 && match.Resource > lastResource)
					Debug.Log(TAG + " INCOME +" + (match.Resource - lastResource) + " → " + match.Resource);
				lastResource = match.Resource;
			}

			// ★ 교전이 *멈춰 있으면* = "화면엔 다 죽은 것 같은데 코어는 아직 살아있다고 센다".
			//   둘의 차이를 눈으로 못 보므로 집계 대상을 좌표·체력째로 찍는다(사용자 실증: 웨이브 2에서 멈춤).
			// ★ 실시간 전환 후 「교전이 길다」는 더 이상 신호가 아니다 — 페이즈가 없어져 *언제나* 교전 중이라
			//   이 검사가 매 판 무조건 경고를 찍었다(거짓 실패는 진짜 실패를 묻는다).
			//   실시간의 고착 신호 = **집계 수가 한참 그대로**(아무도 안 죽고 아무도 안 나온다).
			CheckStall();

			// 이름표 — 인형이 「물건」이 아니라 「아이」가 됐나. 스폰이 코루틴이라 첫 확인은 관찰 루프에서.
			if (dollsReported == false && match.DollLabels.Count > 0)
			{
				dollsReported = true;
				Debug.Log(TAG + " DOLL-NAMES count=" + match.DollLabels.Count + " first=" + match.DollLabels[0].Text);
			}

			// 영웅이 생기면 한 번 보낸다 — 스폰이 코루틴이라 「지금 없다」가 「이 판엔 없다」가 아니다.
			if (heroCommanded == false && match.HasHero)
			{
				heroCommanded = true;
				heroProbeFrom = match.HeroPosition;
				heroProbeTarget = heroProbeFrom + new Vector3(5f, 0f, 5f);
				heroProbeReady = match.CommandHero(heroProbeTarget);
				heroProbeAt = now;
				Debug.Log(TAG + " HERO commanded=" + heroProbeReady + " from=" + heroProbeFrom + " to=" + heroProbeTarget);
			}

			// 영웅이 명령한 쪽으로 실제로 가까워졌나 — 「명령을 받았다」와 「움직였다」는 다른 사실이다.
			if (heroProbeReady && now - heroProbeAt > 1.5)
			{
				heroProbeReady = false;
				float wasDistance = Vector3.Distance(heroProbeFrom, heroProbeTarget);
				float nowDistance = Vector3.Distance(match.HeroPosition, heroProbeTarget);
				if (nowDistance < wasDistance - 0.5f)
					Debug.Log(TAG + " HERO-MOVE-OK " + wasDistance.ToString("F1") + " → " + nowDistance.ToString("F1"));
				else
					Debug.LogError(TAG + " HERO-MOVE-FAIL 명령했는데 안 움직임 "
						+ wasDistance.ToString("F1") + " → " + nowDistance.ToString("F1"));
			}

			// ★ 마릿수 성능 실측 — 「수백 마리」를 원했는데 지금까지 확인된 건 수십이다. 재지 않으면
			//   늘려도 되는지 모르고, 모르면 못 늘린다. 프레임 시간과 살아있는 마릿수를 같이 찍는다.
			frameSamples++;
			frameTimeSum += Time.unscaledDeltaTime;
			if (now - lastPerfLog >= PERF_LOG_INTERVAL && frameSamples > 0)
			{
				float averageMs = frameTimeSum / frameSamples * 1000f;
				int aliveNow = match.AliveEnemyCount;
				if (aliveNow > perfPeakAlive)
					perfPeakAlive = aliveNow;

				// ★ 소음도 여기서 같이 찍는다. 사격 소음은 *전투 중에만* 나는데, 따로 둔 창은
				//   판이 도는 시점에 열려 전투 전에 닫혀 버렸다(두 번 그렇게 「못 쟀다」로 끝났다).
				//   이 표본은 마수가 살아 있는 동안 되풀이되므로 쏘는 소리를 못 놓친다.
				Debug.Log(TAG + " PERF alive=" + aliveNow + " peak=" + perfPeakAlive
					+ " frameMs=" + averageMs.ToString("F1")
					+ " fps=" + (averageMs > 0f ? (1000f / averageMs).ToString("F0") : "-")
					+ " noise=" + (match != null ? match.LoudestNoise.ToString("F1") : "-")
					// ★ 「깨어 있고 걸을 수 있는 땅인데 안 간다」의 다음 갈림길 — 길찾기가 답을 못 준 것인지,
					//   탐색 상한에 걸린 것인지. 판이 끝난 뒤엔 못 물어보므로 도는 동안 같이 찍는다.
					+ " noPath=" + (match != null ? match.NavigatorNoPathCount.ToString() : "-")
					+ " capHit=" + (match != null ? match.PathCapHits.ToString() : "-")
					+ " peak=" + (match != null ? match.PathPeakCells.ToString() : "-")
					+ " shots=" + (match != null ? match.ShotsReported.ToString() : "-"));

				lastPerfLog = now;
				frameSamples = 0;
				frameTimeSum = 0f;
			}

			if (now - lastSample >= SAMPLE_INTERVAL)
			{
				lastSample = now;
				Transform stageRoot = FindStageRoot();
				int aliveEnemies = CountEnemiesNear(stageRoot);
				if (aliveEnemies > 0 && firstContactWave < 0)
				{
					firstContactWave = match.WaveIndex;
					Debug.Log(TAG + " FIRST-WAVE-SPAWNED wave=" + match.WaveIndex + " enemies=" + aliveEnemies);
				}
			}

			// 이벤트(신호)와 상태(사실)를 둘 다 본다 — 재시작이 매치를 Dispose/Begin 하며 구독이 끊기는 경로가
			// 있으면 이벤트만 믿는 검증은 "안 끝났다"고 오판한다. Outcome 이 ground truth.
			bool outcomeEnded = match.Outcome != TowerDefenseOutcome.InProgress;
			bool ended = matchEndedSeen || outcomeEnded;

			if (ended || now - observeStart > OBSERVE_SECONDS)
			{
				Debug.Log(TAG + " SUMMARY endedEvent=" + matchEndedSeen
					+ " endedOutcome=" + outcomeEnded
					+ " outcome=" + match.Outcome
					+ " wavesCleared=" + match.WaveIndex
					+ " resource=" + match.Resource
					+ " firstWaveSpawned=" + (firstContactWave >= 0)
					+ " observed=" + (now - observeStart).ToString("F1") + "s");

				// ★ 게임은 *끝나야* 게임이다. 관찰만 하고 끝내면 "결말이 오는가"를 영영 검증 못 한다
				//   (지금까지 패배를 한 번도 관측한 적이 없었다). 결말 → 배너 → 다시 도전까지 한 사이클을 닫는다.
				if (ended == false)
				{
					Debug.LogError(TAG + " CONCLUSION-FAIL 관찰 " + OBSERVE_SECONDS + "s 동안 매치가 끝나지 않음 "
						+ "— 승리도 패배도 없으면 게임이 아니라 무한 루프다. phase=" + match.Phase
						+ " coreAlive=" + (match.CoreCombatant != null && match.CoreCombatant.IsAlive));
					Finish();
					return;
				}

				restartAt = now;
				step = Step.VerifyConclusion;
			}
		}

		/// <summary>
		/// 방어 있는 교전 관측 — 격파 보상(마수 1기당 즉시 자원)이 실제로 들어오는지 본다.
		/// 「잡는 맛」은 교전 도중 자원이 오르는지로만 검증된다(웨이브 정산은 교전이 끝나야 오므로 구분됨).
		/// </summary>
		private static void ObserveDefended(double now)
		{
			const double DEFENDED_SECONDS = 55.0;

			if (match == null)
			{
				// ★ 바로 옆 관찰 경로는 이걸 구분하는데 여기만 안 했다 — Play 가 끝났으면 매치가 죽은 게
				//   아니라 *씬이 통째로 내려간* 것이다. 하네스 종료 사유이지 게임 결함이 아니다.
				//   구분 안 하면 매 판 빨간 줄이 두 개씩 쌓이고, 그 잡음이 진짜 실패를 덮는다(실제로 덮었다).
				if (EditorApplication.isPlaying == false)
					Debug.LogWarning(TAG + " DEFENDED-END Play 가 관찰 도중 종료됨(씬 언로드) — 게임 결함 아님, 관찰 조기 중단.");
				else
					Debug.LogError(TAG + " DEFENDED-FAIL Play 중인데 매치가 사라졌다.");
				Finish();
				return;
			}

			if (match.Resource != defendedLastResource)
			{
				if (defendedLastResource >= 0 && match.Resource > defendedLastResource)
				{
					int gain = match.Resource - defendedLastResource;
					bool duringAssault = match.Phase == TowerDefensePhase.Assault;
					if (duringAssault)
						killIncomeEvents++;
					Debug.Log(TAG + " GAIN +" + gain + " → " + match.Resource
						+ " phase=" + match.Phase + " aliveEnemies=" + match.AliveEnemyCount
						+ (duringAssault ? "  (교전 중 = 격파 보상)" : "  (정산)"));
				}
				defendedLastResource = match.Resource;
			}

			// ★ 첫 웨이브는 사람이 부를 때까지 안 온다(의도) — 하네스도 「사람」 역할을 해야 한다.
			//   동시에 그 관문이 진짜 걸리는지 여기서 증명한다: 기본 건설 시간(8초)을 훌쩍 넘겨도
			//   여전히 Prepare 면 시계가 안 도는 것이 맞다.
			if (match.IsWaitingForFirstCall)
			{
				if (now - defendedStart > 12.0 && firstWaveCalled == false)
				{
					firstWaveCalled = true;
					Debug.Log(TAG + " FIRST-WAVE-GATE 12초가 지나도 Prepare 유지 — 자동으로 안 넘어감 ✔ 이제 호출");
					match.RequestNextWave();
				}
				return;
			}

			DumpWaveVariety(now);

			// 고착 진단은 관찰 구간이 어디든 *같은 한 곳*이 판정한다(사본을 두면 한쪽만 고쳐진다).
			CheckStall();

			if (now - defendedStart < DEFENDED_SECONDS)
				return;

			int pierceHits = 0;
			int splashHits = 0;
			int slowApplied = 0;
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>(FindObjectsInactive.Include))
			{
				pierceHits += weapon.PierceHits;
				splashHits += weapon.SplashHits;
				slowApplied += weapon.SlowApplied;
			}
			Debug.Log(TAG + " TOWER-EFFECTS pierce=" + pierceHits + " splash=" + splashHits + " slow=" + slowApplied);

			TowerDefenseAdaptationState adaptation = match.Adaptation;
			Debug.Log(TAG + " ADAPT slow=" + adaptation.SlowResist.ToString("F2")
				+ " splash=" + adaptation.SplashResist.ToString("F2")
				+ " pierce=" + adaptation.PierceResist.ToString("F2")
				+ " note=\"" + TowerDefenseAdaptation.Describe(adaptation) + "\""
				+ " (상한 " + TowerDefenseAdaptation.MAX_RESIST + " — 봉인 X)");

			// 전초기지는 정수(정산에서만 나옴)로 서므로 *웨이브를 몇 번 넘긴 뒤*에 확인해야 한다.
			VerifyOutpost();

			string verdict = TAG + " DEFENDED-RESULT killIncomeEvents=" + killIncomeEvents
				+ " wave=" + match.WaveIndex + " resource=" + match.Resource
				+ " nextIncome=" + match.NextWaveIncome + " harvesters=" + match.HarvesterCount;

			if (killIncomeEvents > 0)
				Debug.Log(verdict + " → 마수를 잡을 때마다 자원이 들어온다 ✔");
			else
				Debug.LogError(verdict + " → 교전 중 보상이 한 번도 안 들어옴(격파 보상 미작동).");

			step = Step.DisarmRestart;
		}

		/// <summary>
		/// 웨이브가 뜨면 그 판의 마수를 종류째 찍는다 — 「종류가 진짜 다르게 나오는가」는 체력·덩치가
		/// 실제로 갈리는지로만 확인된다(색만 다르고 스탯이 같으면 종류는 착시다).
		/// </summary>
		private static void DumpWaveVariety(double now)
		{
			if (match == null || match.Phase != TowerDefensePhase.Assault)
				return;

			if (match.WaveIndex != lastDumpedWave)
			{
				lastDumpedWave = match.WaveIndex;
				waveDumpAt = now + 1.5; // 스폰 코루틴이 끝날 시간을 준다.
				return;
			}

			if (waveDumpAt < 0.0 || now < waveDumpAt)
				return;
			waveDumpAt = -1.0;

			int index = 0;
			foreach (ICombatant combatant in match.WaveEnemies)
			{
				if (combatant == null)
					continue;
				Transform enemyTransform = ((MonoBehaviour)combatant).transform;
				Debug.Log(TAG + " VARIETY wave=" + match.WaveIndex + " [" + index + "]"
					+ " hp=" + combatant.Hp + "/" + combatant.HpMax
					+ " scale=" + enemyTransform.localScale.x.ToString("F2")
					+ " alive=" + combatant.IsAlive);
				index++;
			}
		}
	}
}
