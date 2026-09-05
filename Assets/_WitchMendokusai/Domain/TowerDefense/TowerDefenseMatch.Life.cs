using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	// TowerDefenseMatch 의 판의 시작과 끝 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch : MonoBehaviour
	{
		public void Begin(TowerDefenseStageSO stageConfig, Transform root)
		{
			stage = stageConfig;
			stageRoot = root;

			// 진행 방식 기본값은 스테이지가 정하지만, 플레이어가 한 번 고르면 그 선택이 재시작을 넘어 유지된다.
			if (waveModeInitialized == false && stage != null)
			{
				autoAdvanceWaves = stage.AutoAdvanceWavesDefault;
				waveModeInitialized = true;
			}

			Begin();
		}

		public void Begin()
		{
			if (started)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 이미 진행 중 — 중복 Begin 무시(재진입 가드).");
				return;
			}
			if (stage == null || stageRoot == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage/stageRoot 미할당 — 시작 불가.");
				return;
			}
			if (stage.CoreUnit == null || stage.CoreUnit.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: stage.CoreUnit/Prefab 미할당 — 코어 없이 시작 불가.");
				return;
			}

			// 코어의 성장 곡선을 스테이지에서 받아 세운다 — 판마다 다시 세우므로 지난 판의 레벨이 새 판으로 새지 않는다.
			coreProgress = new TowerDefenseBuildingProgress(stage.CoreLevelBaseCost, stage.CoreLevelGrowth);

			started = true;
			StartCoroutine(BeginRoutine());
		}

		private IEnumerator BeginRoutine()
		{
			// 의존은 TowerDefenseModeController.Construct 가 넘긴다. 없으면 배선 누락 (fail-fast)
			if (pool == null || timeManager == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: ObjectPoolManager/TimeManager 미배선 . TowerDefenseModeController.Construct 가 Construct 를 불러야 한다.");
				started = false;
				yield break;
			}

			PrepareLayout(); // 판을 먼저 확정 — 지면·노드·스폰·길안내가 전부 여기서 파생된다.
			BuildGround();

			targeting = new TargetingSystem();

			// ★ 난이도는 *시작 조건*이다 — 규칙을 갈라 쓰지 않고 숫자만 곱한다(갈라 쓰면 다른 게임이 된다).
			difficulty = TowerDefenseDifficulty.For(Difficulty);
			TowerDefenseRules scaledRules = stage.Rules;
			scaledRules.StartingResource = Mathf.Max(1, Mathf.RoundToInt(scaledRules.StartingResource * difficulty.StartingResourceScale));
			scaledRules.StartingLives = Mathf.Max(1, Mathf.RoundToInt(scaledRules.StartingLives * difficulty.LivesScale));
			scaledRules.PressurePerMinute *= difficulty.PressureScale;
			scaledRules.FirstWaveEnemyCount = Mathf.Max(1, Mathf.RoundToInt(scaledRules.FirstWaveEnemyCount * difficulty.EnemyCountScale));

			core = new TowerDefenseCore(scaledRules)
			{
				AutoAdvance = autoAdvanceWaves,
				FirstAutoWave = stage.ManualFirstWave ? 1 : 0,
			};
			// 새 판 = 새 세대. 지난 판이 부르던 인형이 뒤늦게 도착해도 이 숫자가 갈라준다.
			matchGeneration++;

			nextCombatantId = 0;
			matchEndedFired = false;
			claimedNodes.Clear(); // 재진입 — 지난 매치의 노드 점유가 새 매치로 새는 것 방지.
			bountyPaidEnemyIds.Clear();
			enemyBountyById.Clear();
			harvesterTransforms.Clear();
			harvesterIsOuter.Clear();
			supplyChain.Clear();
			outposts.Clear();
			DisconnectedHarvesters = 0;
			LabCount = 0;
			RefreshAvailableSlots(); // 판이 열릴 때의 해금 상태 — 처음엔 채집뿐이다.
			TrapsSpent = 0;
			speedStep = 1;
			lastRunningStep = 1;
			ApplySpeed();
			occupiedCells.Clear(); // 재진입 — 지난 매치의 셀 점유가 새 매치로 새는 것 방지.

			// 새 판 = 새 선택·새 이름·새 영웅. 하나라도 남으면 "새 판"이 아니다.
			boons.Reset();
			dollLabels.Clear();
			soldDolls.Clear();
			nextDollOrdinal = 0;
			// 연구로 쌓은 것도 판과 함께 끝난다 — 안 지우면 다음 판이 지난 판의 연구를 물고 시작한다
			// (코어 성장과 같은 병. 「새 판」이라면 아무것도 안 남아야 한다).
			ClearResearch();

			heroActive = false;
			heroTransform = null;
			heroMovement = null; // 남겨두면 다음 판이 지난 판의 몸을 붙잡고 걷게 시킨다.
			heroCombatant = null;
			heroVisionSourceIndex = -1;
			heroRespawnRemaining = 0f;
			heroVisionCell = new Vector2Int(int.MinValue, int.MinValue);
			enemyMaxStopDistance = 0f;
			nests.Clear();
			nestCombatants.Clear();
			nestsEverSpawned = false;
			NestsDestroyed = 0;
			BuiltCount = 0;
			LostCount = 0;
			KilledCount = 0;
			PeakEnemies = 0;
			LeakedCount = 0;
			windowGrowing = false;
			powerGrid.Clear();
			enemyStillness.Clear();

			yield return SpawnCoreRoutine();
			if (coreCombatant == null)
			{
				// 코어 스폰 자체가 실패 — 이미 로그됨. 진입 상태만 리셋(started 가드 해제).
				started = false;
				yield break;
			}

			yield return SpawnHeroRoutine(); // 영웅 미설정 스테이지면 즉시 빠져나온다(기존 판과 동일).
			yield return SpawnNestsRoutine(); // 마수가 나오는 자리를 *부술 수 있는 것*으로 세운다.
			yield return SpawnLairsRoutine(); // 판 곳곳에 잠든 마수 — 넓히는 행위 자체를 위험으로 만든다.

			timeManager.RegisterCallback(Tick);
			ticking = true;

			// 이어하기가 예약돼 있으면 여기서 되살린다(값을 먼저 맞추고 건물을 한 채씩).
			if (pendingRestore != null)
			{
				TowerDefenseSaveData restore = pendingRestore;
				pendingRestore = null;
				yield return RestoreRoutine(restore);
			}
		}

		/// <summary>
		/// 이번 매치의 판 확정 — 절차 생성이면 생성기를 돌리고, 아니면 스테이지 SO 의 고정값을 그대로 담는다.
		/// 어느 쪽이든 결과는 같은 active* 목록이라 매치 본문에는 분기가 없다(분기를 여기저기 흩으면
		/// 언젠가 한 곳이 옛 경로를 보고 조용히 어긋난다).
		/// </summary>
		private void PrepareLayout()
		{
			activeSpawnPoints.Clear();
			activeNodePositions.Clear();
			activeNodeIncomeMultipliers.Clear();
			activeNodeIsOuter.Clear();
			mapLayout = null;
			flowField = null;
			flowNavigator = null;

			if (stage.UseProceduralMap == false)
			{
				// 스테이지 SO 는 디자이너가 인스펙터에서 적는 자리(엔진 쪽)라 값을 들일 때 캐스트한다 (TASK-WM-214).
			activeCorePosition = stage.CorePosition.ToSim();
				activeGroundWidth = stage.GroundWidth;
				activeGroundLength = stage.GroundLength;

				if (stage.EnemySpawnPoints != null)
					for (int spawnIndex = 0; spawnIndex < stage.EnemySpawnPoints.Length; spawnIndex++)
			{
				activeSpawnPoints.Add(stage.EnemySpawnPoints[spawnIndex].ToSim());
			}
				if (stage.ResourceNodePositions != null)
				{
					foreach (UnityEngine.Vector3 nodeWorldPosition in stage.ResourceNodePositions)
					{
						activeNodePositions.Add(nodeWorldPosition.ToSim());
						activeNodeIncomeMultipliers.Add(1f);
						activeNodeIsOuter.Add(false);
					}
				}
				return;
			}

			TowerDefenseMapParameters parameters = stage.MapParameters;
			if (stage.RandomizeSeedEachMatch)
				parameters.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

			// ★ 지정된 씨앗이 있으면 그 판을 그대로 다시 만든다 — 「이 씨앗 해봐」가 성립하는 자리.
			//   판 전체가 씨앗 하나에서 태어나므로, 판을 나누는 데 드는 것이 숫자 한 줄뿐이다.
			//   한 번 쓰면 지운다(다음 판까지 계속 같은 땅이면 그건 고정이지 공유가 아니다).
			if (nextMatchSeed.HasValue)
			{
				parameters.Seed = nextMatchSeed.Value;
				nextMatchSeed = null;
			}

			// 이어하기면 그 판의 씨앗을 그대로 쓴다 — 같은 땅이 다시 나와야 내 건물이 제자리에 선다.
			destroyedNestPositions.Clear();
			if (pendingRestore != null)
			{
				// 부순 둥지는 *세우기 전에* 알아야 한다 — 세운 뒤 지우면 한 프레임이라도 되살아난다.
				if (pendingRestore.DestroyedNestPositions != null)
					destroyedNestPositions.AddRange(pendingRestore.DestroyedNestPositions);

				parameters.Seed = pendingRestore.MapSeed;
				if (pendingRestore.MapWidth > 0 && pendingRestore.MapLength > 0)
				{
					parameters.Width = pendingRestore.MapWidth;
					parameters.Length = pendingRestore.MapLength;
				}
			}

			mapLayout = TowerDefenseMapGenerator.Generate(parameters);

			activeCorePosition = mapLayout.CorePosition;
			activeGroundWidth = mapLayout.GroundWidth;
			activeGroundLength = mapLayout.GroundLength;
			activeSpawnPoints.AddRange(mapLayout.EnemySpawnPoints);
			foreach (TowerDefenseResourceNodeSpot node in mapLayout.ResourceNodes)
			{
				activeNodePositions.Add(node.Position);
				activeNodeIncomeMultipliers.Add(node.IncomeMultiplier);
				activeNodeIsOuter.Add(node.Tier == TowerDefenseNodeTier.Outer);
			}

			// 길 안내판 — 암반이 생긴 순간 직선 이동은 벽에 박힌다(웨이브가 영원히 안 끝나는 그 사고).
			wallCells.Clear();
			flowField = new TowerDefenseFlowField(
				mapLayout.Width, mapLayout.Length, mapLayout.CoreCell, IsPathBlocked);
			gridPath = new TowerDefenseGridPath(mapLayout.Width, mapLayout.Length, IsPathBlocked);
			flowNavigator = new TowerDefensePathNavigator(
				mapLayout, gridPath, stageRoot, stage.GroundCellSize * 2f, stage.EnemyCornerSmoothing);

			vision = new TowerDefenseVision(mapLayout.Width, mapLayout.Length);
			visionSources.Clear();

			Debug.Log($"{nameof(TowerDefenseMatch)}: 판 생성 seed={mapLayout.Seed} "
				+ $"암반={mapLayout.ObstacleCells.Count}칸 노드={mapLayout.ResourceNodes.Count} 스폰={mapLayout.EnemySpawnPoints.Count}");
		}

		/// <summary>
		/// 판이 끝났나 — 끝난 판에는 아무것도 더 못 짓는다.
		///
		/// ★ 라이브에서 잡았다: 목숨이 0 이 되어 결말 화면이 떠 있는데도 건물이 계속 세워졌다.
		///   끝난 판에 손을 대면 「무엇이 그 성적을 만들었나」가 흐려지고(요약은 끝난 시점을 말하는데
		///   화면엔 그 뒤에 세운 것이 서 있다), 다시 도전을 누르기 전까지 판이 끝난 것도 안 끝난 것도
		///   아닌 상태가 된다. 끝은 끝이어야 한다.
		/// </summary>
		public bool IsMatchOver => Outcome != TowerDefenseOutcome.InProgress;

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			TickHero();

			// ★ 실시간이라 카드가 걸려도 판은 멈추지 않는다(사용자 지시, 데아빌). 멈추고 싶으면 사람이
			//   직접 멈춘다(⏸ 버튼) — 시간을 쥐는 것은 시스템이 아니라 플레이어다.
			CullEscapedEnemies(); // 무대 밖 개체가 웨이브를 영원히 붙잡지 못하게 — 집계 *전에* 정리.
			CullLeakedEnemies();  // 목표에 닿은 마수는 사라지고 목숨이 준다(유출제).
			UnstickEnemies();     // 굳은 마수를 풀어준다 — 한 마리가 굳으면 웨이브가 영영 안 끝난다.
			CullDestroyedNests(); // 부순 둥지의 출구를 닫는다 — 「버틴다」가 「밀어낸다」가 되는 자리.
			WakeNearbyLairs();    // 내 것이 가까이 갔으면 잠든 서식지가 깨어난다.
			AnnounceAdaptation(); // 마수가 무엇에 익숙해졌는지 — 안 보이면 없는 규칙이다.
			AnnouncePressure();   // 시간이 올린 강도 — 같은 포탑이 갑자기 안 통하는 이유.
			TickLairLeash();      // 깨어난 것은 제 자리를 지킨다 — 코어로 행진하면 그냥 파도가 하나 더다.
			CollectClearedLairs();// 다 쓴 서식지는 정수를 낸다 — 싸워서 버는 길.
			TrackLostBuildings(); // 내 것이 부서지면 *그 자리*를 알린다 — 화면 밖이면 알 길이 없었다.
			RefreshPower();       // 전기를 못 받는 건물은 선다(도시 건설의 규칙 그대로).
			TickSignalView();     // 신호가 번지는 것을 눈으로 보여준다 — 테두리와 파동.
			RefreshBuildingProgress(); // 「무엇이 일하고 있나」를 머리 위 바에 채운다.
			TryGrowWindow();      // 내 것이 판 끝에 닿으면 판이 자란다(무한 맵).
			ApplyEnemyVisibility(); // 안 보이는 마수는 화면에서도 지운다(규칙과 그림이 같아야 한다).
			RefreshSupply();        // 방어 건물이 부서지면 그 순간 사슬이 끊긴다.
			PayKillBounties();    // 격파 즉시 보상 — 웨이브 정산만 있으면 교전 중엔 아무 보상도 안 온다.

			bool coreAlive = coreCombatant != null && coreCombatant.IsAlive;
			CountAliveEnemies(); // 죽은 참조 정리 — 세는 값은 아래 집계를 쓴다.

			// ★ 「한때 몇 마리까지」도 *쳐들어온 마수*만 센다. 둥지는 목록에 있지만 쳐들어온 것이 아니다 —
			//   화면의 「적 N마리」만 고쳐놨더니 판 요약은 여전히 둥지 수만큼 부풀어 있었다(같은 병, 다른 자리).
			if (AliveEnemyCount > PeakEnemies)
				PeakEnemies = AliveEnemyCount;

			TowerDefenseSignal signal = core.Tick(TimeManager.TICK, coreAlive);
			switch (signal)
			{
				case TowerDefenseSignal.WaveStarted:
					RefreshVision(); // 어스름 진입/이탈이 시야에 즉시 반영돼야 한다.
					StartCoroutine(SpawnGroupRoutine(ScaledEnemyCount(core.WaveIndex)));
					break;

				// 상시로 한 마리씩 새어 나온다 — 「웨이브 사이엔 안전하다」가 사라진다(데아빌의 배회 감염체).
				case TowerDefenseSignal.TrickleDue:
					StartCoroutine(SpawnGroupRoutine(1));
					break;

				// 정산은 시계가 돈다 — 웨이브를 격퇴해야 벌던 옛 구조에서는 실시간에 아무것도 안 들어온다.
				case TowerDefenseSignal.IncomeDue:
					ShowIncomeBreakdown();
					HealDefenders();
					AwardHarvestExperience(); // 캐는 것도 일이다 — 채집도 자란다.
					AwardCoreExperience(stage.HarvestExperience);
					break;
				case TowerDefenseSignal.Victory:
					Conclude(TowerDefenseOutcome.Victory);
					break;
				case TowerDefenseSignal.Defeat:
					Conclude(TowerDefenseOutcome.Defeat);
					break;
				// (구 페이즈제 잔재 — 실시간에서는 안 온다.)
				case TowerDefenseSignal.WaveCleared:
					break;
				// None = 규칙 상 상태전이 없음 — 셸 actuation 0.
				case TowerDefenseSignal.None:
				default:
					break;
			}

			// ★ 끝났다는 사실은 *신호가 아니라 상태*가 진실이다. 목숨이 다 닳는 패배(유출제 = 지금의 주
			//   패배 경로)는 규칙층이 결과만 적고 신호를 안 내보내서, 화면이 그걸 영영 못 들었다
			//   (실측: outcome=Defeat 인데 배너가 안 뜨고 요약도 안 나옴). 결과를 직접 보면
			//   앞으로 어떤 새 끝 조건이 생겨도 「신호 내는 걸 깜빡해서 화면이 조용한」 일이 안 생긴다.
			if (matchEndedFired == false && core.Outcome != TowerDefenseOutcome.InProgress)
				Conclude(core.Outcome);
		}

		private void Conclude(TowerDefenseOutcome outcome)
		{
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				existingTimeManager.RemoveCallback(Tick);

			// 종료 = 브레인(core)이 actuation 정지 권한 행사 — 전 드라이버 정지(좀비 틱 방지). ArenaMatch 와 동형.
			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}

			if (matchEndedFired == false)
			{
				matchEndedFired = true;
				MatchEnded(outcome);
			}
		}

		/// <summary>
		/// 매치 생명주기 정리 — 단일 경로 + *재진입 가능*(ArenaMatch.Dispose 와 동형): 틱 콜백 해제 +
		/// 드라이버 정지/클리어 + targeting unregister + 스폰 유닛 풀 반환 + 지면 파괴 + 진입 상태 리셋.
		/// 컬렉션 비움으로 멱등(Dispose→Destroy→OnDestroy 이중 호출 무해).
		/// StopAllCoroutines 를 최우선 호출 — 진행 중이던 스폰 코루틴(웨이브/배치)이 pool/targeting/core 필드
		/// null화 이후 재개돼 NRE 나는 것을 원천 차단(스펙#A belt). 코루틴 내부 yield 직후 null 가드는 braces.
		/// </summary>
		public void Dispose()
		{
			StopAllCoroutines();
			ticking = false;

			if (TimeManager.TryGetExistingInstance(out TimeManager existingTimeManager))
				existingTimeManager.RemoveCallback(Tick);

			foreach (TacticDriver driver in drivers)
			{
				if (driver != null)
					driver.StopDriving();
			}
			drivers.Clear();

			if (targeting != null)
			{
				foreach (ICombatant combatant in registeredCombatants)
					targeting.Unregister(combatant);
			}
			matchGeneration++; // 판을 접는다 — 진행 중이던 소환은 전부 남의 판 것이 된다.
			registeredCombatants.Clear();
			waveEnemies.Clear();
			claimedNodes.Clear();
			occupiedCells.Clear();

			if (ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager existingPool))
			{
				foreach (GameObject unit in spawnedUnits)
					ReleaseUnit(existingPool, unit);
			}
			spawnedUnits.Clear();

			if (stageRoot != null)
			{
				for (int childIndex = stageRoot.childCount - 1; childIndex >= 0; childIndex--)
					Destroy(stageRoot.GetChild(childIndex).gameObject);
			}

			// ★ 신호장 그림도 여기서 놓는다. 무대 자식은 위에서 전부 파괴하지만 **파괴는 프레임 끝에**
			//   일어나므로 그 사이 이 참조는 아직 살아 있다 — 같은 프레임에 새 판이 시작하면 *죽을 예정인*
			//   그림에 계속 그리게 된다. 참조를 여기서 끊으면 그런 틈이 아예 없다.
			signalView = null;

			// 재진입 — 다음 Begin() 이 새 매치를 돌릴 수 있게 진입 상태 리셋.
			core = null;
			coreCombatant = null;
			targeting = null;
			// pool 과 timeManager 는 비우지 않는다. 컨트롤러가 한 번 넘긴 의존이고 다음 판 Begin 이 그대로 쓴다 (2026-09-05)
			started = false;
		}

		private void OnDestroy()
		{
			Dispose();
		}
	}
}
