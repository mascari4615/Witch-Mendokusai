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
	// TowerDefenseMatch 의 스폰 루틴 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 생명주기 정리(재매치 누수 방지, ArenaMatch 와 동형) — 스폰 유닛/등록 참가자/구동 드라이버 추적 →
		// Dispose 에서 despawn/unregister/정지.
		private readonly List<GameObject> spawnedUnits = new();
		private readonly List<Vector3> activeSpawnPoints = new();

		/// <summary>
		/// 인형 하나가 판에 서기까지의 *공통 절차* 결과 — 코루틴은 값을 못 돌려주므로 담아서 준다.
		/// </summary>
		private sealed class SpawnedUnit
		{
			public GameObject GameObject;
			public UnitObject UnitObject;
			public MatchCombatant Combatant;
			public bool Ok;
		}

		/// <summary>
		/// 인형 하나를 판에 세운다 — 꺼내기부터 표적 등록까지의 아홉 단계.
		///
		/// ★ 왜 한 곳으로 모았나: 코어·마수·둥지·수비대·영웅 다섯 경로가 *같은 아홉 단계*를 각자
		///   되풀이하고 있었다(438줄). 한 경로가 한 줄만 빠뜨리면 그 인형만 혼자 다르게 논다 —
		///   실제로 「스킬 자동시전 끄기」를 빠뜨리면 그 유닛만 제멋대로 스킬을 쏜다(트랩#1).
		/// ★ 한 프레임 양보가 이 안에 있다: 꺼낸 직후 Init 하면 Start 초기화와 겹친다(트랩#4).
		///   그 대기 중에 판이 사라질 수 있어 되돌아온 뒤 반드시 다시 확인한다.
		/// 각 경로가 다른 것(무기·전술·이름표·둥지 체력·영웅 조종권)은 부른 쪽이 뒤에 얹는다.
		/// </summary>
		private IEnumerator SpawnUnitRoutine(Unit unitData, Vector3 worldPosition, int team,
			Color tint, float scale, SpawnedUnit result)
		{
			result.Ok = false;
			int generation = matchGeneration; // 이 인형은 *이 판의 것*이다.

			GameObject unitGameObject = pool.Spawn(unitData.Prefab);
			if (spawnedUnits.Contains(unitGameObject) == false)
				spawnedUnits.Add(unitGameObject); // 풀이 옛 시체를 재사용하면 같은 참조 — 중복 추적 방지.

			// ★ 세운 것은 *움직이지 않는다*. 물리를 안 끄면 영웅·마수가 지나가며 건물을 밀어낸다
			//   (사용자 실증: "코어 건물이 영웅 유닛에 밀립니다. 건물 좀 고정되게"). 칸에 세운 것이
			//   칸 밖으로 밀리면 「그 자리에 지었다」는 규칙 자체가 거짓이 된다 — 점유 칸은 그대로인데
			//   그림만 옆에 가 있다. 여기 한 곳에서 전부 고정한다(스폰의 단일 관문).
			Rigidbody spawnedBody = unitGameObject.GetComponent<Rigidbody>();
			if (spawnedBody != null)
			{
				spawnedBody.isKinematic = true;
				spawnedBody.useGravity = false;
			}

			// ★ 안개는 안 보이는 개체의 렌더러를 *끈다*. 그 개체가 풀로 돌아갔다 재사용되면 꺼진 채로
			//   다시 태어난다 — 사용자 실증: "다시시작 하면 건물 모습이 안보임". 세우는 순간 되켠다.
			//   (끄는 쪽과 켜는 쪽이 짝이 안 맞으면, 그 병은 *다음 판*에 나타나 원인을 찾기 어렵다.)
			foreach (Renderer spawnedRenderer in unitGameObject.GetComponentsInChildren<Renderer>(true))
				spawnedRenderer.enabled = true;
			unitGameObject.transform.position = worldPosition.ToUnity();
			result.GameObject = unitGameObject;

			yield return null; // 트랩#4 — Start 초기화가 가라앉은 뒤 Init.

			// 대기 중 판이 사라졌거나(모드 이탈) *다른 판으로 갈렸으면*(다시 시작) 여기서 멈춘다.
			if (core == null || targeting == null || pool == null || generation != matchGeneration)
			{
				// 꺼내둔 몸은 돌려준다 — 안 돌려주면 아무 판에도 안 속한 인형이 화면에 남는다.
				if (unitGameObject != null && ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager strayPool))
					ReleaseUnit(strayPool, unitGameObject);
				spawnedUnits.Remove(unitGameObject);
				yield break;
			}

			UnitObject unitObject = unitGameObject.GetComponent<UnitObject>();
			if (unitObject == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: {unitData.Prefab.name} 에 UnitObject 없음 — 세우지 못했다.");
				yield break;
			}

			// Init → 트랩#1(자동시전 차단) → MatchCombatant 부여 = 투기장과 공유하는 편입 절차.
			MatchCombatant combatant = CombatUnitSpawner.Enlist(unitObject, unitData, team, nextCombatantId++);

			ApplyReadability(unitObject, tint, scale);
			unitGameObject.SetActive(true);

			// 트랩#2 — 다섯 경로(코어·마수·둥지·수비대·영웅)가 예외 없이 하던 것이라 관문 안으로 들였다.
			// 활성화 *뒤*라야 OnEnable 로 뜬 코루틴이 OnDisable 로 멈춘다.
			CombatUnitSpawner.SilenceBrains(unitGameObject);

			targeting.Register(combatant);
			registeredCombatants.Add(combatant);

			result.UnitObject = unitObject;
			result.Combatant = combatant;
			result.Ok = true;
		}


		private IEnumerator SpawnCoreRoutine()
		{
			SpawnedUnit spawned = new();
			yield return SpawnUnitRoutine(stage.CoreUnit, stageRoot.TransformPoint(activeCorePosition.ToUnity()).ToSim(),
				DEFENDER_TEAM, stage.CoreTint, stage.CoreScale, spawned);
			if (spawned.Ok == false)
				yield break;

			GameObject coreGameObject = spawned.GameObject;
			UnitObject coreUnitObject = spawned.UnitObject;
			MatchCombatant combatant = spawned.Combatant;

			// 여기부터는 *코어만의 것*이다. (트랩#2 브레인 비활성은 세우는 문이 이미 했다.)
			targeting.RegisterObjective(combatant); // 적이 전진할 목표물 — 일반 등록과 직교, 둘 다 필요.

			coreCombatant = combatant;

			// ★ 코어도 반격한다(개선 목록 22번) — 마지막 보루가 무방비면 「여기까지 왔다」가 곧 끝이다.
			//   포탑과 *같은 무기 표*를 쓴다: 다른 표를 두면 두 곳이 갈라져 화면과 규칙이 어긋난다.
			if (stage.CoreWeapon != null)
			{
				TowerDefenseWeapon coreWeapon = coreGameObject.GetComponent<TowerDefenseWeapon>();
				if (coreWeapon == null)
					coreWeapon = coreGameObject.AddComponent<TowerDefenseWeapon>();
				coreWeapon.Configure(stage.CoreWeapon, targeting, combatant, waveEnemies,
					IsVisibleAt, DamageMultiplierFor, () => Adaptation, () => TowerRangeMultiplier);
				coreWeapon.ReportNoise = ReportShotNoise;
			}

			AddVisionSource(coreGameObject.transform.position.ToSim(), stage.CoreVisionRadius);

			// 보급이 여기서 출발해 어디까지 닿는지 — 안 보이면 「왜 안 이어지지」를 짐작으로 풀어야 한다.
			ShowSupplyReachRing(coreGameObject.transform);
		}

		/// <summary> WaveStarted 신호 처리 — SO 스폰 지점에 분산 스폰. 한 마리도 못 내보내면 FastFail 로그. </summary>
		/// <summary>
		/// 마수를 내보낸다. count 가 큰 무리면 웨이브, 1이면 상시로 새어 나오는 한 마리다.
		///
		/// ★ 실시간 전환의 핵심(사용자 지시, 데아빌): 살아있는 마수 목록을 *비우지 않는다*. 페이즈제에서는
		///   「이번 웨이브 것만」 추적하면 됐지만, 실시간에서는 앞 무리와 상시 마수가 동시에 판에 있다.
		///   비우면 아직 살아있는 마수를 놓쳐 화면과 집계가 갈라진다.
		/// </summary>
		private IEnumerator SpawnGroupRoutine(int count)
		{
			PruneDeadEnemies(); // 죽은 것만 걷어낸다 — 살아있는 것은 남긴다(실시간이라 겹쳐 존재한다).

			ComposeWave(core.WaveIndex, waveComposition); // 예고와 같은 함수 = 화면이 말한 대로 나온다.
			RebuildInvasionFront(core.WaveIndex);          // 이번 파도가 밀려올 테두리 토막 — 예고와 같은 함수.

			TowerDefenseWaveEventKind waveEvent = WaveEventAt(core.WaveIndex);
			int enemyCount = count;
			int spawnedCount = 0; // 실제로 UnitObject 확보 + 등록까지 끝난 수 — 이게 0 이면 스테이지 데이터 구멍이다.

			for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
			{
				if (stage.EnemyUnit == null || stage.EnemyUnit.Prefab == null)
				{
					Debug.LogWarning($"{nameof(TowerDefenseMatch)}: stage.EnemyUnit/Prefab 미할당 — 웨이브 스폰 skip.");
					break;
				}

				// ★ **파도는 테두리, 상시로 새는 것은 둥지.**
				//   테두리 침공을 넣으면서 둘 다 테두리로 보냈더니 「둥지를 부수면 그 출구가 닫힌다」가
				//   거짓말이 됐다 — 부숴도 오는 양이 그대로였다(규칙은 바꿨는데 그 약속을 안 옮긴 것).
				//   파도가 테두리에서 오는 것과, 둥지가 상시 압박의 출구인 것은 서로 다른 층이라 둘 다 산다.
				bool isWave = count > 1;
				IReadOnlyList<Vector3> origins = isWave && invasionFront.Count > 0
					? invasionFront
					: activeSpawnPoints;

				// 둥지를 다 부쉈으면 상시로 샐 곳이 없다 — 그게 「출구가 닫혔다」의 실제 모습이다.
				// (여기서 안 막으면 자리가 없어 무대 한가운데(0,0)에서 솟는다.)
				if (isWave == false && origins.Count == 0)
					yield break;
				Vector3 localSpawn = origins.Count > 0
					? origins[enemyIndex % origins.Count] + SpawnSpreadOffset(enemyIndex, origins.Count)
					: Vector3.zero;

				// ★ 분산(SpawnSpreadOffset)이 마수를 암반 위/뒤에 떨구면 그 마리는 「갈 수 없는 자리」에서 시작해
				//   그대로 굳는다 — 한 마리만 굳어도 웨이브가 영영 안 끝난다(사용자 실증: "멈춰서 안올때가 있음").
				//   출현 지점 자체는 길이 보장돼 있으므로(RebuildPathing 검사) 벌어진 자리만 되돌린다.
				localSpawn = SnapSpawnToReachable(localSpawn);

				// 종류를 먼저 정한다 — 색·덩치가 그 종류에서 나오므로 세우기 전에 알아야 한다.
				TowerDefenseEnemyArchetype archetype = enemyIndex < waveComposition.Count
					? EnemyArchetypeAt(waveComposition[enemyIndex])
					: null;

				SpawnedUnit spawned = new();
				yield return SpawnUnitRoutine(stage.EnemyUnit, stageRoot.TransformPoint(localSpawn.ToUnity()).ToSim(), ATTACKER_TEAM,
					archetype != null ? archetype.Tint : stage.EnemyTint,
					stage.EnemyScale * (archetype != null ? archetype.ScaleMultiplier : 1f), spawned);
				if (spawned.Ok == false)
					continue;

				GameObject enemyGameObject = spawned.GameObject;
				UnitObject enemyUnitObject = spawned.UnitObject;
				MatchCombatant enemyCombatant = spawned.Combatant;
				enemyBountyById[enemyCombatant.CombatantId] = archetype != null ? archetype.Bounty : core.BountyPerKill;

				// ★ 스탯 배수는 *켠 다음 프레임*에 씌운다. UnitObject.Start 가 UnitData 로 스탯을 통째 다시
				//   세팅하므로(재-Init 규약), 켜기 전에 올려둔 체력은 첫 프레임에 조용히 원래대로 돌아간다
				//   (라이브 실증: 덩치·보상은 갈리는데 체력만 전부 같았다).
				yield return null;
				if (core == null || targeting == null || pool == null)
					yield break;
				ApplyArchetypeStats(enemyUnitObject, archetype, stage != null ? stage.EnemyMoveSpeedMultiplier : 1f);
				ApplyPressure(enemyUnitObject); // 오래 버틸수록 단단해진다 — 실시간의 난이도는 시간이 올린다.
				ApplyWaveEventStats(enemyUnitObject, waveEvent);


				TacticDriver enemyDriver = enemyUnitObject.GetComponent<TacticDriver>();
				if (enemyDriver == null)
					enemyDriver = enemyUnitObject.gameObject.AddComponent<TacticDriver>();
				enemyDriver.enabled = true; // 풀이 어떤 상태로 주든 켜고 시작한다(허리띠 + 멜빵).
				enemyDriver.Initialize(stage.EnemyTactic, targeting, timeManager);
				IgnoreHeroCollision(enemyUnitObject.gameObject); // 새로 온 마수도 영웅을 통과한다.
				enemyDriver.Navigator = flowNavigator; // 지형이 있으면 돌아가고, 없으면(null) 직선 그대로.
				enemyDriver.StopsToAttack = false;     // 걸으면서 쏜다 — 전진이 멈추면 판이 안 끝난다.
				// 마수가 코어 둘레에 「고리」로 서는 거리 — 유출 반경이 이보다 작으면 바깥 고리는 영영 안 닿는다.
				enemyMaxStopDistance = Mathf.Max(enemyMaxStopDistance, enemyDriver.MaxStopDistance);
				drivers.Add(enemyDriver);

				// 표적 등록은 세우는 문이 이미 했다.
				waveEnemies.Add(enemyCombatant);
				spawnedCount++;

				// ★ 한 지점에 한꺼번에 쏟으면 마수들이 서로의 몸에 끼어 그 자리에서 못 나온다
				//   (라이브 실측: 출현 줄에서 세 마리가 나란히 4초씩 정지). 좌우로 벌리는 것만으로는
				//   마릿수가 늘면 결국 겹친다 — *시간*으로 흘려보내야 구조적으로 안 겹친다.
				//   덤으로 「웨이브가 밀려온다」는 감각이 생긴다(장르 표준의 trickle spawn).
				// ★ 무리로 내보낸다 (사용자 지시: "여러 기가 한 번에 천천히 몰려오게").
				//   무리 안 = 눈에 안 띄는 짧은 간격(0으로 두면 서로의 몸에 끼어 그 자리서 못 나온다 — 실측).
				//   무리가 다 나왔으면 = 긴 간격. 그래서 「덩어리로 밀려오고, 다음 덩어리까지는 숨 돌린다」.
				bool groupFinished = stage.EnemyGroupSize <= 1
					|| spawnedCount % stage.EnemyGroupSize == 0;
				float wait = groupFinished ? stage.EnemySpawnInterval : stage.EnemyGroupSpacing;
				if (wait > 0f)
					yield return new WaitForSeconds(wait);
			}

			// 웨이브를 불렀는데 한 마리도 안 나온 것은 그 자체로 스테이지 데이터 구멍이다 — 조용히 넘어가면
			// 「큰 무리가 왔다」는 화면 글자만 뜨고 판은 텅 빈다. (실시간 전환 뒤 규칙은 살아있는 적 수를
			// 안 보므로 클리어 오인 위험은 사라졌고, 남은 것은 이 FastFail 알림뿐이다.)
			if (spawnedCount == 0 && count > 1)
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 웨이브 적 0마리 스폰 — stage.EnemyUnit/EnemySpawnPoints 확인 필요.");
		}

		private Vector3 SpawnSpreadOffset(int enemyIndex, int pointCount)
		{
			if (pointCount <= 0)
				return Vector3.zero;

			int repeat = enemyIndex / pointCount;          // 이 지점을 몇 번째로 쓰는가
			int lane = (repeat + 1) / 2;                   // 0,1,1,2,2,...
			float side = repeat % 2 == 0 ? 1f : -1f;       // 좌우 번갈아
			float spread = stage.EnemySpawnSpread;

			// z 도 조금 밀어 완전히 같은 줄에 서지 않게(앞뒤로도 벌림).
			return new Vector3(lane * spread * side, 0f, repeat * spread * 0.35f);
		}

		/// <summary>
		/// 벌어진 출현 자리가 길 위인지 확인하고, 아니면 가장 가까운 갈 수 있는 칸으로 되돌린다.
		/// 고정 판(흐름장 없음)에서는 아무것도 안 한다 — 그쪽은 애초에 암반이 없다.
		/// </summary>
		private Vector3 SnapSpawnToReachable(Vector3 localSpawn)
		{
			if (mapLayout == null || flowField == null)
				return localSpawn;

			Vector2Int cell = mapLayout.WorldToCell(localSpawn);
			if (flowField.IsReachable(cell))
				return localSpawn;

			if (TrySnapToReachable(cell, out Vector2Int freeCell) == false)
				return localSpawn;

			return mapLayout.CellToWorld(freeCell);
		}

		/// <summary> 이번 판의 마수 출현 지점(무대 로컬). </summary>
		public IReadOnlyList<Vector3> ActiveEnemySpawnPoints => activeSpawnPoints;

		private IEnumerator SpawnDefensiveUnitRoutine(Unit unitData, TacticProgram tactic, Vector3 worldPosition, bool isHarvester, float incomeMultiplier = 1f, TowerDefenseTowerArchetype towerArchetype = null, bool isOuterNode = false, bool isGenerator = false)
		{
			if (unitData == null || unitData.Prefab == null)
			{
				Debug.LogError($"{nameof(TowerDefenseMatch)}: 배치 유닛 데이터/Prefab 미할당 — 스폰 불가(자원은 이미 차감됨).");
				yield break;
			}

			// 어떤 인형이냐에 따라 색·덩치가 갈린다 — 세우기 전에 정해야 문에 넘길 수 있다.
			Color tint = isGenerator ? stage.GeneratorTint
				: isHarvester ? stage.HarvesterTint
				: (towerArchetype != null ? towerArchetype.Tint : stage.TowerTint);

			SpawnedUnit spawned = new();
			yield return SpawnUnitRoutine(unitData, worldPosition, DEFENDER_TEAM,
				tint, isHarvester ? stage.HarvesterScale : stage.TowerScale, spawned);
			if (spawned.Ok == false)
				yield break; // 자원은 이미 차감됐지만 좀비 스폰은 막는다.

			GameObject unitGameObject = spawned.GameObject;
			UnitObject unitObject = spawned.UnitObject;
			MatchCombatant combatant = spawned.Combatant;


			if (tactic != null)
			{
				TacticDriver driver = unitObject.GetComponent<TacticDriver>();
				if (driver == null)
					driver = unitObject.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(tactic, targeting, timeManager);
				drivers.Add(driver);
			}

			// 표적 등록은 세우는 문이 이미 했다.

			// 세워둔 포탑의 사거리를 옅게 늘 보여준다 — 「어디가 비었나」는 기존 커버리지가 보여야 알 수 있다.
			if (isHarvester == false && isGenerator == false)
			{
				if (towerArchetype != null)
				{
					TowerDefenseWeapon weapon = unitObject.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						weapon = unitObject.gameObject.AddComponent<TowerDefenseWeapon>();
					weapon.Configure(towerArchetype, targeting, combatant, waveEnemies, IsVisibleAt, DamageMultiplierFor, () => Adaptation, () => TowerRangeMultiplier);
					weapon.ReportNoise = ReportShotNoise;
				}

				// 지어놓은 포탑의 원도 연구를 따라 자란다 — 원형 그대로 그리면 총과 원이 갈라진다.
				// ★ 여기는 이미 위에서 채집·발전을 걸러낸 안쪽이다 — 물건별로 원을 갈라 그리는 분기를
				//   여기 두면 절대 안 도는 죽은 코드가 된다(그렇게 넣었다가 라이브에서 「잴 것이 0개」로 드러났다).
				float towerRange = (towerArchetype != null ? towerArchetype.Range : RawTowerRange())
					* TowerRangeMultiplier;
				if (towerRange > 0f)
				{
					// ★ 사거리 원은 *묻는 순간에만* 뜬다(사용자 지시: "계속 보이니까 정신없어").
					//   수십 개가 상시로 겹치면 원이 정보가 아니라 노이즈가 된다 — 마우스를 얹거나
					//   설치 미리보기 중일 때만 켠다. 전부 보고 싶으면 디버그 토글(ShowAllRanges).
					Color ringColor = towerArchetype != null ? towerArchetype.Tint : new Color(0.45f, 0.72f, 1f, 1f);
					ringColor.a = 0.55f;
					TowerDefenseRing ring = TowerDefenseRing.Create(
						unitGameObject.transform, "RangeRing", ringColor, 0.08f, 0.05f);
					ring.SetRadius(towerRange);
					ring.SetVisible(showAllRanges);
					rangeRings.Add(ring);
				}
			}

			AddVisionSource(worldPosition,
				isGenerator ? stage.GeneratorVisionRadius
					: isHarvester ? stage.HarvesterVisionRadius
					: (towerArchetype != null ? towerArchetype.VisionRadius : stage.CoreVisionRadius));

			// 세운 인형에게 이름 — 벽·함정은 물건이지만 인형은 아이다(이 경로로 오는 것은 전부 인형).
			BuiltCount++;
			RegisterDoll(unitGameObject.transform,
				isGenerator ? stage.GeneratorTint
					: isHarvester ? stage.HarvesterTint
					: (towerArchetype != null ? towerArchetype.Tint : stage.TowerTint),
				isHarvester,
				// ★ 저장은 *내가 세운 것*만 되살려야 한다 — 영웅처럼 판이 스스로 만드는 것을 건물로 적으면
				//   이어할 때마다 유령 포탑이 한 채씩 는다(실측: 3채 저장 → 4채 복원).
				isPlacedBuilding: true,
				// ★ 종류를 안 적으면 4종을 세워놨어도 전부 기본형으로 되살아난다.
				variant: TowerArchetypeIndexOf(towerArchetype));

			// 모든 내 건물이 보급 사슬의 징검다리 — 포탑을 늘어놓는 것이 곧 보급선을 잇는 일이 된다.
			supplyChain.Add(unitGameObject.transform);

			if (isGenerator)
				powerGrid.AddGenerator(unitGameObject.transform);

			// 포탑·채집은 전기를 먹는다(발전은 안 먹는다 — 발전이 전기를 먹으면 자기 꼬리를 문다).
			if (isGenerator == false)
				powerGrid.AddConsumer(unitGameObject.transform);

			if (isHarvester)
			{
				harvesterTransforms.Add(unitGameObject.transform);
				harvesterIsOuter[unitGameObject.transform] = isOuterNode;
			}

			RefreshSupply(); // 수입은 「지을 때 더한다」가 아니라 「지금 몇 개가 이어져 있나」로 정해진다.
		}
	}
}
