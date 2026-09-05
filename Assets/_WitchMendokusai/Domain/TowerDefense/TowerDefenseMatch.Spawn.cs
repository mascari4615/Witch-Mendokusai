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
	// TowerDefenseMatch 의 Spawn 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 생명주기 정리(재매치 누수 방지, ArenaMatch 와 동형) — 스폰 유닛/등록 참가자/구동 드라이버 추적 →
		// Dispose 에서 despawn/unregister/정지.
		private readonly List<GameObject> spawnedUnits = new();

		// 매 틱 aliveEnemies 카운트용 — 죽거나 풀 반환된(null) 엔트리는 조회 시 제거(멱등 정리).
		// 웨이브마다 SpawnWaveRoutine 시작에서 비움(이전 웨이브 잔여가 다음 웨이브에 누적되는 것 방지).
		private readonly List<MatchCombatant> waveEnemies = new();

		// 격파 보상을 이미 지급한 마수(CombatantId) — 죽은 개체는 여러 틱 동안 목록에 남으므로 중복 지급 차단.
		// 오브젝트 풀이 같은 GameObject 를 되돌려주기 때문에 참조가 아니라 매치 고유 id 로 센다.
		private readonly HashSet<int> bountyPaidEnemyIds = new();
		private readonly List<Vector3> activeSpawnPoints = new();

		// 이번 파도가 밀려오는 테두리 토막(무대 로컬). 파도마다 다시 뽑히므로 출구가 고정되지 않는다
		// = 「길」이 안 생긴다. 비어 있으면 옛 고정 둥지 방식으로 되돌아간다.
		private readonly List<Vector3> invasionFront = new();

		// 이번(또는 다음) 웨이브의 마수 구성 — 원소 = EnemyArchetypes 인덱스. 결정론이라 화면 예고와 실제 스폰이 같다.
		private readonly List<int> waveComposition = new();

		// 격파 보상은 종류마다 다르다(단단한 놈일수록 크게) — 죽은 뒤엔 어떤 종류였는지 알 수 없으므로
		// 스폰 시점에 CombatantId → 보상액을 기록해 둔다.
		private readonly Dictionary<int, int> enemyBountyById = new();

		// 웨이브 자동 진행 여부 — 플레이 중 토글되므로 코어(진행 중)와 필드(다음 매치)를 함께 갱신한다.
		// 재시작해도 방금 고른 방식이 유지돼야 한다(설정을 매번 다시 고르게 만들지 않는다).
		private bool autoAdvanceWaves = true;
		private bool waveModeInitialized;

		public bool AutoAdvanceWaves
		{
			get => autoAdvanceWaves;
			set
			{
				autoAdvanceWaves = value;
				if (core != null)
					core.AutoAdvance = value;
			}
		}

		/// <summary> 다음 웨이브 호출(수동 진행 / 자동에서도 즉시 시작). 건설 국면이 아니면 false. </summary>
		public bool RequestNextWave() => core != null && core.RequestNextWave();

		/// <summary> 수동 진행에서 호출이 예약된 상태인지 — HUD 표시용. </summary>
		public bool IsNextWaveRequested => core != null && core.IsNextWaveRequested;

		/// <summary>
		/// 이번 웨이브 적 추적 목록(읽기 전용) — **진단용**. "다 잡은 것 같은데 안 넘어간다"는
		/// 곧 "코어가 세는 생존자와 화면에서 보이는 것이 다르다"는 뜻이라, 무엇이 살아 있다고
		/// 집계되는지를 좌표·체력까지 직접 볼 수 있어야 원인을 짚는다(추측 금지).
		/// </summary>
		public IReadOnlyList<MatchCombatant> WaveEnemies => waveEnemies;

		/// <summary>
		/// 코어가 보는 생존 적 수 — HUD 표시 + 진단 대조용. 매 프레임 읽히므로 목록을 건드리지 않는
		/// **순수 집계**(정리는 코어 틱의 CountAliveEnemies 가 담당 — 표시가 상태를 바꾸면 안 된다).
		/// </summary>
		public int AliveEnemyCount
		{
			get
			{
				int count = 0;
				foreach (MatchCombatant combatant in waveEnemies)
				{
					if (combatant == null || combatant.IsAlive == false)
						continue;
					// ★ 둥지는 이 목록에 *포탑이 쏘라고* 들어 있다 — 쳐들어오는 마수가 아니다.
					//   같이 세면 화면의 「적 N마리」가 둥지 수만큼 늘 거짓말을 하고(실측 +8),
					//   「아무도 안 죽는다」를 보는 진단도 절대 안 움직이는 8개에 묻힌다.
					if (nestCombatants.Contains(combatant))
						continue;
					count++;
				}
				return count;
			}
		}
		public int WaveIndex => core != null ? core.WaveIndex : 0;

		/// <summary> 다음 정산액 + 가동 채집 인형 수 — 「채집 인형이 뭐 하는 놈인지」를 화면이 말하는 근거 숫자. </summary>
		public int NextWaveIncome => core != null ? core.NextWaveIncome : 0;
		public int NextWaveEssence => core != null ? core.NextWaveEssence : 0;

		/// <summary>
		/// 길 다시 계산 + 표시 갱신. 모든 출현 지점에서 코어까지 갈 수 있으면 true.
		/// 흐름장이 이미 있어 재계산이 싸다 — 벽을 세울 때마다 전부 다시 그려도 부담이 없다.
		/// </summary>
		/// <summary>
		/// 코어 둘레에 *여러 진입점*을 목표로 더한다 — 「사방에서 넓은 면으로 밀려온다」의 근본.
		///
		/// ★ 왜 필요한가 (사용자 실측: "여전히 거의 한 줄", "떼거지로"): 목표가 코어 한 점이면
		///   모든 길이 그 한 점으로 수렴한다. 같은 거리의 길 중 내 것을 고르게 해도, *정확한 대각선*
		///   방향에서는 최단 경로가 하나뿐이라 못 흩어진다(시험으로 확인한 한계).
		///   목표를 코어를 감싼 고리로 나누면, 마수마다 *가장 가까운 진입점*이 달라져서 마지막까지
		///   갈라진 채 다가온다 — 길찾기는 그대로 최단이고, 벽도 그대로 돈다.
		/// ★ 막힌 칸은 안 넣는다 — 못 가는 곳을 목표로 두면 그 방향이 통째로 죽는다.
		/// </summary>
		private void AddApproachRing(Vector2Int coreCell)
		{
			int radius = Mathf.Max(1, stage.CoreApproachRingCells);
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					// 고리 = 정사각 테두리만. 안쪽까지 채우면 코어 주변이 통째로 목표라 뜻이 없다.
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
						continue;

					Vector2Int cell = new Vector2Int(coreCell.x + dx, coreCell.y + dy);
					if (mapLayout.IsInside(cell) == false || IsPathBlocked(cell))
						continue;
					pathGoals.Add(cell);
				}
			}
		}

		/// <summary>
		/// 판 테두리의 어느 지점에서든 코어까지 닿는가.
		///
		/// ★ 파도가 매번 다른 토막에서 오므로 「그 토막만」 검사할 수는 없다 — 지금 막아둔 벽은 *나중*
		///   파도에도 그대로 남기 때문이다. 테두리를 고르게 훑어 하나라도 갇히면 그 벽은 거절한다.
		/// 각도 간격은 노출값 — 촘촘할수록 안전하고 그만큼 판정이 무겁다.
		/// </summary>
		private bool IsBorderReachable()
		{
			if (mapLayout == null || flowField == null)
				return true;

			// ★ 「테두리의 *모든* 점이 닿아야 한다」로 만들었더니 **벽이 하나도 안 섰다**(실측: placed=0).
			//   테두리에는 원래 암반이 박혀 있어서, 내 벽과 무관하게 못 닿는 점이 늘 있다 —
			//   새로 만든 자물쇠가 주인을 막은 것이다.
			// 진짜로 막아야 하는 것은 「한 방향이 통째로 봉인되는 것」이다. 출현 자리는 어차피
			//   갈 수 있는 칸으로 스냅되므로, **방위마다 한 곳이라도 닿으면** 그 방향은 살아 있다.
			float step = Mathf.Max(1f, stage.BorderCheckStepDegrees);
			for (int sector = 0; sector < 8; sector++)
			{
				float from = sector * 45f;
				bool anyReachable = false;

				for (float angle = from; angle < from + 45f; angle += step)
				{
					Vector3 local = TowerDefenseWaveOrigin.BorderPoint(
						angle, activeGroundWidth * 0.5f, activeGroundLength * 0.5f, stage.InvasionEdgeInset);

					if (flowField.IsReachable(mapLayout.WorldToCell(local)) == false)
						continue;

					anyReachable = true;
					break;
				}

				if (anyReachable == false)
					return false; // 이 방위가 통째로 막혔다 — 그쪽에서 올 파도가 갇힌다.
			}
			return true;
		}



		/// <summary>
		/// 화면에서 즉시 읽히게 만드는 공통 처리 — 역할 색 + 한 칸 크기 + 애니메이터 정지.
		///
		/// ★ 애니메이터 정지가 핵심(실측): 프리팹 '[Sprite] Unit' 에 슬라임 애니메이터가 붙어 있어
		///   매 프레임 sprite 를 자기 클립으로 덮어쓴다 → 유닛 데이터의 그림을 아무리 넣어도 다음
		///   프레임에 슬라임으로 되돌아갔다(사용자 실증 2회 "여전히 슬라임"). 끄지 않으면 어떤 시각
		///   구분도 무의미.
		/// ★ 색 = 정체: 아트가 아직 없으므로 역할 4색을 서로 멀게 잡고, HUD 범례가 같은 색을 읽어
		///   화면에 이름을 띄운다(색↔이름 단일 소스 — 둘이 어긋나면 안내가 거짓말이 된다).
		/// ★ 크기 = 격자 한 칸: 칸보다 크면 서로 밀치고 소속도 안 읽힌다.
		/// </summary>
		/// <summary>
		/// 종류별 체력·속도 적용 — 기반 유닛 스탯에 배수를 씌운다. 새 유닛 에셋 없이 「단단한 놈/빠른 놈」이
		/// 성립하는 지점. HP_MAX_STAT(기반)까지 같이 올려야 이후 스탯 재계산이 원래 값으로 되돌리지 않는다.
		/// 리스는 ApplyReadability 가 이미 잡아뒀다(같은 스폰 경로) — 반납 시 원본 스탯으로 복원된다.
		/// </summary>
		private static void ApplyArchetypeStats(UnitObject unitObject, TowerDefenseEnemyArchetype archetype, float paceScale)
		{
			if (unitObject == null || archetype == null)
				return;

			if (Mathf.Approximately(archetype.HealthMultiplier, 1f) == false)
			{
				int scaledMax = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.HP_MAX] * archetype.HealthMultiplier));
				unitObject.UnitStat[UnitStatType.HP_MAX_STAT] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_MAX] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_CUR] = scaledMax;
			}

			// 판 전체 속도 배수 — 종류별 배수와 곱해진다(느린 놈은 더 느리게, 빠른 놈도 함께 느려진다).
			float paceMultiplier = archetype.SpeedMultiplier * paceScale;
			if (Mathf.Approximately(paceMultiplier, 1f) == false)
			{
				int scaledSpeed = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] * paceMultiplier));
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = scaledSpeed;
			}
		}

		private void ApplyReadability(UnitObject unitObject, Color tint, float scale)
		{
			if (unitObject == null)
				return;

			// 손대기 전 원본 스냅샷 — 반납 시 그대로 되돌린다(다시 시작해도 지난 매치 흔적 0).
			AcquireLease(unitObject);

			foreach (Animator animator in unitObject.GetComponentsInChildren<Animator>(true))
				animator.enabled = false;

			if (unitObject.SpriteRenderer != null)
				unitObject.SpriteRenderer.color = tint;

			unitObject.transform.localScale = (Vector3.one * scale).ToUnity();

			// ★ 몸집을 키우면 *충돌 몸통도 같이 커진다* — 그러면 단단한 마수(1.35배)는 암반과 벽 사이
			//   좁은 틈에 끼어 나오지 못한다(사용자 실증: "단단한 마수 아까부터 껴서 못 움직인다").
			//   이동은 콜라이더를 쓸어서 미끄러지는 방식이라, 몸통이 한 칸보다 크면 길이 있어도 못 지난다.
			//   보이는 크기는 그대로 두고 *충돌 몸통만* 원래 굵기로 되돌린다 — 「단단함」은 체력이 말한다.
			CapsuleCollider capsule = unitObject.GetComponent<CapsuleCollider>();
			if (capsule != null && scale > 1f)
			{
				capsule.radius /= scale;
				capsule.height /= scale;
			}
		}

		/// <summary> 영웅과 지금 살아있는 마수들의 몸싸움을 서로 무시시킨다(길막 방지). </summary>
		private void IgnoreCollisionsWithEnemies(GameObject hero)
		{
			if (hero == null)
				return;

			foreach (ICombatant enemy in waveEnemies)
			{
				if (enemy is MonoBehaviour behaviour && behaviour != null)
					IgnorePair(hero, behaviour.gameObject);
			}
		}

		private static void IgnorePair(GameObject left, GameObject right)
		{
			if (left == null || right == null || left == right)
				return;

			Collider[] leftColliders = left.GetComponentsInChildren<Collider>(true);
			Collider[] rightColliders = right.GetComponentsInChildren<Collider>(true);
			foreach (Collider leftCollider in leftColliders)
			{
				foreach (Collider rightCollider in rightColliders)
				{
					if (leftCollider != null && rightCollider != null)
						Physics.IgnoreCollision(leftCollider, rightCollider, true);
				}
			}
		}

		/// <summary>
		/// 풀 반납 단일 경로 — 반납 *전에* 원상복구(<see cref="TowerDefenseUnitLease.Release"/>).
		/// 이걸 거치지 않고 Despawn 하면 다음 매치가 지난 매치의 색·크기·정지된 애니메이터·역할 드라이버를
		/// 그대로 물려받는다(코어/포탑/채집/마수가 같은 프리팹 = 같은 풀이라 역할까지 섞인다).
		/// </summary>
		private static void ReleaseUnit(ObjectPoolManager targetPool, GameObject unit)
		{
			if (unit == null)
				return;

			TowerDefenseUnitLease lease = unit.GetComponent<TowerDefenseUnitLease>();
			if (lease != null)
				lease.Release(unit.GetComponent<UnitObject>());

			// ★ **끈 것은 켜서 돌려준다.** 서식지 목줄이 전술을 잠시 꺼두는데, 그 상태로 풀에 들어가면
			//   그 몸을 재사용한 *다음 마수*가 꺼진 채로 태어나 영영 안 움직인다 — 한 마리만 굳어도
			//   파도가 안 끝나던 그 사고와 같은 종류다. 풀은 남의 상태를 기억하면 안 된다.
			TacticDriver driver = unit.GetComponent<TacticDriver>();
			if (driver != null)
				driver.enabled = true;

			// ★ 소속도 끊어서 돌려준다. 안 끊으면 이 몸으로 되살아난 *파도 마수*를 옛 서식지가
			//   집으로 끌어당긴다(실측 「집에서 95~123, 목줄 20」). 반납 지점이 여기 하나뿐이라
			//   여기서 끊는 것이 빠뜨릴 자리가 없는 유일한 방법이다.
			TowerDefenseLairMember member = unit.GetComponent<TowerDefenseLairMember>();
			if (member != null)
				member.Leave();

			targetPool.Despawn(unit);
		}

		/// <summary>
		/// 전술이 꺼진 채 살아 있는 마수 수 — 0 이 아니면 누군가 상태를 켜서 안 돌려준 것이다.
		/// (목줄이 잠시 끄는 것은 *서식지 마수*뿐이고 그건 제 자리를 지키는 중이라 정상이므로 뺀다.)
		/// </summary>
		public int FrozenEnemyCount
		{
			get
			{
				int frozen = 0;
				foreach (MatchCombatant enemy in waveEnemies)
				{
					if (enemy == null || enemy.IsAlive == false)
						continue;
					if (IsSleepingLairGuard(enemy) || IsAwakenedLairGuard(enemy))
						continue;

					TacticDriver driver = enemy.GetComponent<TacticDriver>();
					if (driver != null && driver.enabled == false)
						frozen++;
				}
				return frozen;
			}
		}
		public int InvasionFrontCount => invasionFront.Count;

		/// <summary> 깨기 전에 「소리가 크다」고 미리 알린 횟수 — 대응할 기회를 줬는지의 창. </summary>
		public int NoiseWarnings { get; private set; }

		// 마지막으로 알린 적응 — 같은 말을 매 프레임 다시 띄우지 않기 위해.
		private string lastAdaptationNote = string.Empty;

		// 마지막으로 알린 강도 단계 — 같은 단계를 다시 알리지 않기 위해.
		private int lastPressureStep = -1;

		/// <summary> 지금 적응이 무엇이라 말하는가 — 하네스가 「보이는가」를 잴 때 기준으로 쓴다. </summary>
		public string AdaptationNote => TowerDefenseAdaptation.Describe(Adaptation);

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

		/// <summary>
		/// 같은 출현 지점에 나오는 마수들을 서로 벌린다.
		///
		/// ★ 겹쳐 스폰하면 물리가 파고듦을 해소하려고 서로를 튕겨내 **맵 밖으로 날려버린다**
		///   (실측: 살아있는 마수 2기가 (1236, -2906, 2015) 로 날아가 웨이브가 영원히 안 끝났다).
		///   출현 지점 수보다 마수가 많아지는 후반 웨이브에서 반드시 발생하므로 스폰 단계에서 막는다.
		/// 같은 지점을 쓰는 몇 번째인지로 좌우 지그재그 — 결정적(같은 웨이브 → 같은 배치).
		/// </summary>
		/// <summary>
		/// waveIndex 파도가 밀려올 테두리 토막을 다시 뽑는다. 스폰과 예고가 **같은 함수**를 쓰므로
		/// 화면이 가리킨 쪽과 실제로 오는 쪽이 갈라질 수 없다(갈라지면 예고가 거짓말이 된다).
		/// </summary>
		private void RebuildInvasionFront(int waveIndex)
		{
			invasionFront.Clear();
			if (stage == null || stage.BorderInvasion == false)
				return;

			TowerDefenseWaveOrigin.SampleAt(
				InvasionAngleAt(waveIndex),
				stage.InvasionArcDegrees,
				activeGroundWidth * 0.5f,
				activeGroundLength * 0.5f,
				stage.InvasionEdgeInset,
				stage.InvasionFrontPoints,
				invasionFront);

			// ★ 테두리는 암반이 흔하다. 그대로 뱉으면 그 마리는 못 걷는 칸에서 시작해 4초 뒤
			//   「못 나아감」 경고를 남기고 굳는다 — 실측 콘솔 경고 30개 중 21개가 이것이었고
			//   좌표가 전부 판 가장자리였다. 스폰 직전 스냅은 반경이 좁아 암반 띠를 못 벗어난다.
			//   전선을 만들 때 *코어 쪽으로 밀어* 걸을 수 있는 첫 칸을 잡는다.
			PullFrontInsideWalkable();
		}

		/// <summary> 전선의 각 점을 코어 쪽으로 밀어 걸을 수 있는 자리로 옮긴다. </summary>
		private void PullFrontInsideWalkable()
		{
			if (mapLayout == null || flowField == null)
				return;

			for (int index = 0; index < invasionFront.Count; index++)
			{
				Vector3 point = invasionFront[index];
				if (flowField.IsReachable(mapLayout.WorldToCell(point)))
					continue;

				Vector3 inward = (Vector3.zero - point).normalized; // 무대 로컬에서 코어는 원점이다.
				for (int step = 1; step <= FRONT_PULL_STEPS; step++)
				{
					Vector3 candidate = point + inward * (step * FRONT_PULL_DISTANCE);
					if (flowField.IsReachable(mapLayout.WorldToCell(candidate)) == false)
						continue;

					invasionFront[index] = candidate;
					break;
				}
			}
		}

		/// <summary> 안쪽으로 미는 한 걸음 길이와 최대 걸음 수 — 암반 띠를 벗어날 만큼은 되어야 한다. </summary>
		private const float FRONT_PULL_DISTANCE = 2f;
		private const int FRONT_PULL_STEPS = 12;

		/// <summary>
		/// 그 파도가 들어오는 방향(도). 화면 예고가 이걸 그대로 읽는다 — 미래 파도도 물어볼 수 있다.
		///
		/// ★ 뚫린 자리가 있으면 그쪽으로 끌린다 — 「지킬 수 있는 만큼만 넓혀라」를 말이 아니라 규칙으로
		///   만드는 자리다. 예고와 스폰이 **같은 이 함수**를 봐야 한다. 갈라지면 화면이 북이라 하고
		///   마수는 남에서 오는, 준비 자체가 무의미해지는 거짓말이 된다.
		/// </summary>
		public float InvasionAngleAt(int waveIndex)
		{
			float baseAngle = TowerDefenseWaveOrigin.AngleDegrees(waveIndex, MapSeed);
			if (stage == null || stage.BreachPull <= 0f || coreCombatant == null)
				return baseAngle;
			if (breach.TryGetBiasAngle(coreCombatant.Position, out float biasAngle) == false)
				return baseAngle;

			return TowerDefenseWaveOrigin.Blend(baseAngle, biasAngle, stage.BreachPull);
		}

		/// <summary> 지금 뜨거운 뚫린 자리 수 — 화면·검사가 「규칙이 살아 있나」를 볼 창. </summary>
		public int BreachHotCount => breach.HotCount;

		/// <summary>
		/// 판의 시계를 앞으로 감는다 — 검사 전용.
		///
		/// ★ 왜 필요한가: 마수 강도는 *시간*이 올린다. 한 칸 오르는 데 실제로 몇 분이 걸려서
		///   하네스가 도는 1~2분 안에는 절대 안 오른다 — 그래서 「강도가 올랐다」는 알림이
		///   여태 한 번도 화면에 안 떴고, 계산만 시험으로 덮인 채 남아 있었다.
		///   재는 쪽이 사건을 일으킬 수 있어야 닫힌다(적응·뚫린 자리에서 두 번 통한 방법).
		/// ★ 이어하기가 쓰는 것과 **같은 문**(시계 되돌리기)으로 들어간다 — 다른 문을 새로 뚫으면
		///   검사만 통과하는 길이 생긴다.
		/// </summary>
		/// <summary> 1분당 강도 상승폭 — 검사가 「몇 초를 감아야 한 칸 오르나」를 역산한다(초 박기 금지). </summary>
		public float PressurePerMinute => stage != null ? stage.Rules.PressurePerMinute : 0f;

		/// <summary> 부서진 자리는 잊히지 않는다 — 다음 파도가 그쪽으로 끌린다. </summary>
		private readonly TowerDefenseBreach breach = new();

		/// <summary> 내가 낸 소리 — 자는 것을 깨운다. </summary>
		private readonly TowerDefenseNoise noise = new();

		/// <summary> 지금 판에서 가장 시끄러운 소리 — 화면·검사가 「규칙이 도나」를 볼 창. </summary>
		public float LoudestNoise => noise.LoudestLevel;

		/// <summary> 서식지가 깨어나는 소리 문턱 · 거리 — 검사가 값을 박지 않고 판에서 읽는다. </summary>
		public float NoiseWakeThreshold => stage != null ? stage.NoiseWakeThreshold : 0f;
		public float NoiseFromShotForVerification => stage != null ? stage.NoiseFromShot : 0f;

		/// <summary> 그 자리에서 들리는 소리 — 검사가 「둥지가 들을 만한가」를 직접 잰다. </summary>
		public float NoiseHeardAt(Vector3 worldPosition)
		{
			return stage != null ? noise.LevelAt(worldPosition, stage.NoiseHearingRadius) : 0f;
		}

		/// <summary>
		/// 소리를 낸다 — 짓기·사격·얻어맞기가 전부 이 문으로 들어온다.
		///
		/// ★ 문을 하나로 두는 이유: 소리를 내는 자리가 늘어날 때마다 합치는 거리·상한을 각자
		///   정하면, 어떤 소리는 자리를 스무 개 만들고 어떤 소리는 하나로 뭉친다. 규칙이 갈라진다.
		/// </summary>
		/// <summary> 한 발의 소리 — 무기가 부르는 통로. 값은 판 자산이 정한다(무기에 박지 X). </summary>
		private void ReportShotNoise(Vector3 worldPosition)
		{
			ShotsReported++;
			if (stage == null || stage.NoiseFromShot <= 0f)
				return;
			EmitNoise(worldPosition, stage.NoiseFromShot);
		}

		/// <summary>
		/// 「쏜 것을 알린」 횟수 — 검사가 「소리가 0 인 이유」를 가르는 유일한 창.
		/// 0 이면 통로가 안 불린 것(죽은 배선)이고, 0 이 아닌데 소리가 0 이면 값이나 잦아듦 문제다.
		/// 둘은 고치는 자리가 전혀 다른데 화면에는 똑같이 「조용함」으로 보인다.
		/// </summary>
		public int ShotsReported { get; private set; }

		public void EmitNoise(Vector3 worldPosition, float amount)
		{
			if (stage == null)
				return;
			noise.Emit(worldPosition, amount, stage.NoiseMergeDistance);
		}

		/// <summary>
		/// 다음 파도의 성격 이름 + 조사("떼거리가"). 성격이 없으면 빈 문자열.
		///
		/// ★ 이 값은 계산은 되는데 *화면에 도달하지 못하고 있었다* — 웨이브 미리보기 칸을 숨기면서
		///   같이 묻혔다(숫자를 안 띄우기로 한 결정의 부작용). 성격은 **말**이라 숫자 금지와 무관하고,
		///   「무엇이 오는가」를 모르면 대비가 성립하지 않는다.
		/// </summary>
		public string NextWaveEventPhrase()
		{
			return TowerDefenseWaveEvent.SubjectPhrase(WaveEventAt(WaveIndex + 1));
		}

		/// <summary> 다음 파도가 오는 쪽 이름("북동" 등). 숫자 대신 말로 예고하기 위한 값. </summary>
		public string NextInvasionDirectionName()
		{
			return TowerDefenseWaveOrigin.DirectionName(InvasionAngleAt(WaveIndex + 1));
		}

		/// <summary> 테두리 침공이 실제로 켜져 돌고 있는가 — 화면이 예고를 띄울지 정하는 근거. </summary>
		public bool IsBorderInvasion => stage != null && stage.BorderInvasion;

		/// <summary>
		/// 다음 파도가 들어올 자리(월드). 화면이 여기에 표식을 세워 **어디를 막을지**를 미리 말한다.
		/// 스폰과 같은 함수를 쓰므로 표식이 선 자리가 곧 실제로 나올 자리다.
		/// </summary>
		public void CollectNextInvasionPoints(List<Vector3> into)
		{
			if (into == null)
				return;

			into.Clear();
			if (stage == null || stage.BorderInvasion == false || stageRoot == null)
				return;

			TowerDefenseWaveOrigin.Sample(
				WaveIndex + 1,
				MapSeed,
				stage.InvasionArcDegrees,
				activeGroundWidth * 0.5f,
				activeGroundLength * 0.5f,
				stage.InvasionEdgeInset,
				stage.InvasionFrontPoints,
				into);

			for (int index = 0; index < into.Count; index++)
				into[index] = stageRoot.TransformPoint(into[index].ToUnity()).ToSim();
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

		/// <summary>
		/// 무대를 벗어난 적 정리 — 지면 아래로 떨어졌거나 개척지 밖으로 날아간 개체는 죽은 것으로 친다.
		///
		/// ★ 이게 없으면 *어떤* 물리 사고든 곧바로 「웨이브가 영원히 안 끝남」이 된다(코어는 생존 적을
		///   세는데 그 적은 화면 밖에 있어 플레이어가 손쓸 방법이 없다). 원인을 하나 막는 것과 별개로,
		///   무대 밖 개체가 진행을 막지 못하게 하는 안전망이 진행 규칙 쪽에 있어야 한다.
		/// </summary>
		private void CullEscapedEnemies()
		{
			if (stage == null || stageRoot == null)
				return;

			float halfWidth = activeGroundWidth * 0.5f + stage.StageBoundsMargin;
			float halfLength = activeGroundLength * 0.5f + stage.StageBoundsMargin;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;

				Vector3 local = stageRoot.InverseTransformPoint(enemy.Position.ToUnity()).ToSim();
				bool escaped = local.y < stage.StageFloorDepth
					|| Mathf.Abs(local.x) > halfWidth
					|| Mathf.Abs(local.z) > halfLength;
				if (escaped == false)
					continue;

				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 마수가 무대를 이탈 — 제거로 처리 local={local}. "
					+ "(스폰 겹침·물리 튕김 흔적이면 EnemySpawnSpread 확인)");

				targeting.Unregister(enemy);
				registeredCombatants.Remove(enemy);
				waveEnemies.RemoveAt(index);

				TacticDriver driver = enemy.GetComponent<TacticDriver>();
				if (driver != null)
					driver.StopDriving();

				ReleaseUnit(pool, enemy.gameObject);
				spawnedUnits.Remove(enemy.gameObject);
			}
		}

		/// <summary> 이번 판의 마수 출현 지점(무대 로컬). </summary>
		public IReadOnlyList<Vector3> ActiveEnemySpawnPoints => activeSpawnPoints;

		/// <summary>
		/// 그 유닛이 무엇인지 사람 말로(툴팁). 화면에 서 있는 것이 「무엇이고 얼마나 버티는지」를 물어볼
		/// 수단이 없으면, 색과 크기만으로 짐작해야 한다(사용자 요청: 유닛 툴팁).
		/// 모르는 대상이면 빈 문자열 — 아무거나 지어내지 않는다.
		/// </summary>
		public string DescribeUnit(MatchCombatant combatant)
		{
			if (combatant == null || combatant.UnitObject == null)
				return string.Empty;

			Transform unit = combatant.transform;
			int currentHp = combatant.UnitObject.UnitStat[UnitStatType.HP_CUR];
			int maxHp = combatant.UnitObject.UnitStat[UnitStatType.HP_MAX];

			// 마수 — 지금 얼마나 남았고 잡으면 얼마인지.
			if (combatant.TeamId == ATTACKER_TEAM)
			{
				string bounty = enemyBountyById.TryGetValue(combatant.CombatantId, out int reward)
					? "  ·  잡으면 +" + Mathf.RoundToInt(reward * boons.BountyMultiplier)
					: string.Empty;
				return "마수\n체력 " + currentHp + " / " + maxHp + bounty;
			}

			TowerDefenseDollLabel label = FindDollLabel(unit);
			string name = label != null ? label.Name : "인형";

			// ★ 코어를 *제일 먼저* 가린다 (WM-200 실측). 코어도 무기를 들고 있어서 아래 포탑 가지가
			//   먼저 낚아채 갔고, 그 아래 코어 설명은 통째로 죽은 가지였다 — 화면엔 「인형 …
			//   같은 자리에 같은 종류를 또 지으면 승급」이라 떴다. 코어는 다시 못 짓는데.
			//   무엇인가(역할)는 무엇을 들었나(무기)보다 앞선다.
			if (coreCombatant == combatant)
			{
				TowerDefenseWeapon coreWeapon = unit.GetComponent<TowerDefenseWeapon>();
				return "코어\n체력 " + currentHp + " / " + maxHp
					+ (coreWeapon != null
						? "\n사거리 " + coreWeapon.Range.ToString("0.#") + "  ·  피해 " + coreWeapon.CurrentDamage
						: string.Empty)
					+ "\n여기까지 새면 목숨이 준다";
			}

			// 포탑 — 무기가 붙어 있으면 그 수치가 정본이다(화면과 규칙이 같은 곳을 읽는다).
			TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
			if (weapon != null)
			{
				bool isHero = HasHero && heroTransform == unit;
				return (isHero ? name + " (영웅)" : name + (label != null && label.Level > 1 ? " ★" + label.Level : ""))
					+ "\n체력 " + currentHp + " / " + maxHp
					+ "\n사거리 " + weapon.Range.ToString("0.#") + "  ·  피해 " + weapon.CurrentDamage
					+ (isHero ? "\n핫바에서 「영웅 이동」을 고르고 찍으면 그리 간다" : "\n같은 자리에 같은 종류를 또 지으면 승급");
			}

			// 채집 인형 — 무엇을 얼마나 캐고, 이어져 있는지.
			if (harvesterTransforms.Contains(unit))
			{
				bool outer = harvesterIsOuter.TryGetValue(unit, out bool isOuter) && isOuter;
				bool connected = label == null || label.Disconnected == false;
				return name + " (채집 인형)"
					+ "\n체력 " + currentHp + " / " + maxHp
					+ "\n" + (outer ? "정수" : "자원") + " ×" + HarvesterMultiplierOf(unit).ToString("0.0")
					+ "\n" + (connected ? "보급 이어짐" : "⚠ 보급 끊김 — 한 푼도 안 들어온다");
			}

			return name + "\n체력 " + currentHp + " / " + maxHp;
		}

		/// <summary>
		/// 흐른 시간만큼 마수를 단단하게 + 카드로 고른 감속을 건다.
		///
		/// ★ 왜 시간인가: 실시간에서 웨이브는 시계가 부른다 — 웨이브 수로 난이도를 올리면 플레이어가
		///   무엇을 하든 똑같이 오른다. 「빨리 정리했다」와 「겨우 버텼다」가 구분되지 않는다.
		///   시간으로 올리면 *오래 끌수록 아프다* 가 되어 둥지를 부수러 나갈 이유가 생긴다.
		/// ★ 상한을 두는 이유: 무한히 오르면 어느 순간부터는 무엇을 해도 지는 판이 된다 — 그건 난이도가
		///   아니라 타이머다.
		/// </summary>
		private void ApplyPressure(UnitObject enemyUnit)
		{
			if (enemyUnit == null || core == null)
				return;

			float pressure = core.Pressure;
			if (pressure > 1f)
			{
				int scaledHp = Mathf.Max(1, Mathf.RoundToInt(enemyUnit.UnitStat[UnitStatType.HP_MAX] * pressure));
				enemyUnit.UnitStat[UnitStatType.HP_MAX] = scaledHp;
				enemyUnit.UnitStat[UnitStatType.HP_CUR] = scaledHp;
			}

			// 카드로 고른 「무거운 걸음」은 *앞으로 나오는* 마수에만 걸린다(이미 걷는 것을 늦추면
			// 고른 순간 판이 통째로 멎어 선택이 아니라 버튼이 된다).
			float speedMultiplier = boons.EnemySpeedMultiplier;
			if (speedMultiplier < 1f)
			{
				enemyUnit.UnitStat[UnitStatType.MOVEMENT_SPEED] =
					Mathf.Max(1, Mathf.RoundToInt(enemyUnit.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedMultiplier));
			}
		}
		public int LeakedCount { get; private set; }

		/// <summary> 지금 마수에 걸린 압력 — 화면이 「점점 세진다」를 말한다. </summary>
		public float Pressure => core != null ? core.Pressure : 1f;

		/// <summary>
		/// 지금까지 내가 쓴 수단의 누적 — 세워둔 포탑들이 각자 센 것을 모은다.
		/// 「무엇을 많이 썼나」가 곧 마수가 무엇에 익숙해지는가다.
		/// </summary>
		public TowerDefenseAdaptationState Adaptation
		{
			get
			{
				if (stage == null)
					return default;

				int slowUses = 0;
				int splashHits = 0;
				int pierceHits = 0;
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit == null)
						continue;
					TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						continue;
					slowUses += weapon.SlowApplied;
					splashHits += weapon.SplashHits;
					pierceHits += weapon.PierceHits;
				}

				return TowerDefenseAdaptation.From(slowUses, splashHits, pierceHits, stage.AdaptationSensitivity);
			}
		}

		/// <summary> waveIndex 파의 성격 — 예고와 스폰이 같은 함수를 본다. </summary>
		public TowerDefenseWaveEventKind WaveEventAt(int waveIndex)
		{
			return stage != null
				? TowerDefenseWaveEvent.For(waveIndex, stage.WaveEventEvery)
				: TowerDefenseWaveEventKind.None;
		}

		/// <summary> 성격까지 반영한 그 웨이브의 마수 수(떼거리는 배로, 정예는 절반). </summary>
		public int ScaledEnemyCount(int waveIndex)
		{
			if (stage == null)
				return 0;

			float scaled = stage.Rules.EnemiesInWave(waveIndex)
				* TowerDefenseWaveEvent.CountScale(WaveEventAt(waveIndex));
			return Mathf.Max(1, Mathf.RoundToInt(scaled));
		}

		// 웨이브 성격을 마수 스탯에 얹는다 — 종류(archetype) 배수 *위에* 곱해지므로 둘이 겹쳐 쌓인다.
		private static void ApplyWaveEventStats(UnitObject unitObject, TowerDefenseWaveEventKind kind)
		{
			if (unitObject == null || kind == TowerDefenseWaveEventKind.None)
				return;

			float healthScale = TowerDefenseWaveEvent.HealthScale(kind);
			if (Mathf.Approximately(healthScale, 1f) == false)
			{
				int scaledMax = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.HP_MAX] * healthScale));
				unitObject.UnitStat[UnitStatType.HP_MAX_STAT] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_MAX] = scaledMax;
				unitObject.UnitStat[UnitStatType.HP_CUR] = scaledMax;
			}

			float speedScale = TowerDefenseWaveEvent.SpeedScale(kind);
			if (Mathf.Approximately(speedScale, 1f) == false)
			{
				int scaledSpeed = Mathf.Max(1, Mathf.RoundToInt(unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedScale));
				unitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] = scaledSpeed;
			}
		}

		/// <summary> 등록된 마수 종류 수(0 이면 기반 유닛 한 종류로 동작). </summary>
		public int EnemyArchetypeCount => stage != null && stage.EnemyArchetypes != null ? stage.EnemyArchetypes.Length : 0;

		/// <summary> index 번 마수 종류(범위 밖이면 null). HUD 범례·예고가 이름·색을 읽는다. </summary>
		public TowerDefenseEnemyArchetype EnemyArchetypeAt(int index)
		{
			if (index < 0 || index >= EnemyArchetypeCount)
				return null;
			return stage.EnemyArchetypes[index];
		}

		/// <summary>
		/// waveIndex 파의 구성을 계산해 result 에 담는다 — *예고*와 *실제 스폰*이 같은 함수를 쓰므로
		/// 화면이 거짓말할 수 없다(예고용 별도 계산을 두면 언젠가 반드시 어긋난다).
		/// </summary>
		public void ComposeWave(int waveIndex, List<int> result)
		{
			result.Clear();
			if (stage == null || core == null)
				return;

			int enemyCount = stage.Rules.EnemiesInWave(waveIndex);
			int archetypeCount = EnemyArchetypeCount;
			if (archetypeCount <= 0)
			{
				for (int index = 0; index < enemyCount; index++)
					result.Add(0);
				return;
			}

			int[] unlockWaves = new int[archetypeCount];
			int[] weights = new int[archetypeCount];
			for (int index = 0; index < archetypeCount; index++)
			{
				TowerDefenseEnemyArchetype archetype = stage.EnemyArchetypes[index];
				unlockWaves[index] = archetype != null ? archetype.UnlockWave : 0;
				weights[index] = archetype != null ? archetype.Weight : 0;
			}

			TowerDefenseWaveComposer.Compose(unlockWaves, weights, waveIndex, enemyCount, result);
		}

		/// <summary>
		/// 목표에 닿은 마수 처리 — 유출(leak). 그 마수는 *사라지고* 목숨이 하나 준다.
		///
		/// ★ 코어를 갉는 방식과 다른 점: 「아직 얼마 남았나」가 아니라 「한 마리라도 새면 아프다」가 된다.
		///   길목 하나가 뚫리는 순간의 무게가 여기서 정해진다. 새 놈이 코어에 눌어붙어 화면에서
		///   사라지던 옛 문제도 같이 없어진다(닿는 즉시 치우므로).
		/// </summary>
		/// <summary>
		/// 실제로 「샜다」로 치는 반경 — 설정값과 *마수가 멈춰 서는 거리* 중 큰 쪽.
		///
		/// ★ 왜 이게 필요한가 (사용자 실증: "몬스터가 멈춰서 안올때가 있음", 라이브 재현 170초):
		///   유출제에서 마수는 코어에 「닿으면」 사라진다. 그런데 마수는 코어를 *때리는 무기*를 갖고 있어서
		///   자기 사거리에 들어오는 순간 **거기서 멈춰 선다**. 그 사거리가 유출 반경보다 크면 마수는
		///   영원히 닿지 않고, 살아있는 마수가 0이 안 되니 **웨이브가 영영 안 끝난다**.
		///   「닿았다」의 기준을 마수가 실제로 멈추는 거리에서 뽑으면 두 숫자가 갈라질 수 없다.
		/// </summary>
		private float EffectiveLeakRadius
		{
			get
			{
				float stopDistance = 0f;
				if (stage != null && stage.EnemyTactic.Rules != null)
				{
					foreach (TacticRule rule in stage.EnemyTactic.Rules)
					{
						if (rule.Target.MaxRange > stopDistance)
							stopDistance = rule.Target.MaxRange;
					}
				}

				// 마수가 실제로 멈추는 자리는 둘 중 더 먼 쪽이다: 「사거리에 들어와서」 또는 「고리로 둘러싸서」.
				// 둘 다 덮지 않으면 바깥에 선 마수가 영영 안 닿아 웨이브가 끝나지 않는다(실측 2회).
				stopDistance = Mathf.Max(stopDistance, enemyMaxStopDistance);
				return Mathf.Max(stage.LeakRadius, stopDistance + stage.LeakRangeMargin);
			}
		}

		// 이번 매치 마수들이 목표에서 멈춰 서는 최대 거리 — 스폰 때 드라이버가 알려준 값.
		private float enemyMaxStopDistance;

		private void CullLeakedEnemies()
		{
			if (core == null || core.UsesLives == false || coreCombatant == null)
				return;

			float leakRadius = EffectiveLeakRadius;
			float leakRadiusSqr = leakRadius * leakRadius;

			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (IsAtAnyGoal(enemy.Position, leakRadiusSqr) == false)
					continue;

				PopWorldText("-1", enemy.Position, TextType.Warning);
				LeakedCount++;
				core.RegisterLeak();

				targeting.Unregister(enemy);
				registeredCombatants.Remove(enemy);
				waveEnemies.RemoveAt(index);

				TacticDriver driver = enemy.GetComponent<TacticDriver>();
				if (driver != null)
					driver.StopDriving();

				ReleaseUnit(pool, enemy.gameObject);
				spawnedUnits.Remove(enemy.gameObject);
			}
		}

		/// <summary>
		/// 시야 밖 마수는 안 그린다 — 포탑이 못 쏘는데 화면에는 보이면, 「왜 안 쏘지」가 버그로 읽힌다.
		/// 규칙(못 쏨)과 그림(안 보임)이 같은 사실을 말해야 한다.
		/// </summary>
		private void ApplyEnemyVisibility()
		{
			if (vision == null)
				return;

			foreach (MatchCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.UnitObject == null)
					continue;

				bool seen = IsVisibleAt(enemy.Position);
				foreach (Renderer enemyRenderer in enemy.UnitObject.GetComponentsInChildren<Renderer>(true))
				{
					if (enemyRenderer.enabled != seen)
						enemyRenderer.enabled = seen;
				}
			}
		}

		// 굳은 마수 감시 — CombatantId → (마지막 자리, 그 자리에 머문 시간).
		private readonly Dictionary<int, (Vector3 Position, float Seconds)> enemyStillness = new();

		/// <summary>
		/// 굳은 마수를 풀어준다(사용자 실증: "몬스터가 멈춰서 안올때가 있음").
		///
		/// ★ 왜 이게 치명적인가: 웨이브 종료 조건이 「살아있는 마수 0」이라 **한 마리만 굳어도 판이 영영 안 끝난다**.
		///   무대 밖 이탈(CullEscapedEnemies)은 이미 막고 있지만, *무대 안에서 제자리에 붙는* 경우는 안 잡혔다.
		/// ★ 왜 굳는가: 스폰 분산이 마수를 암반 칸 위/뒤에 떨궈 흐름장이 「거기서는 갈 수 없다」고 답하면
		///   안내가 끊기고, 그 자리에서 직선으로 벽을 밀며 영원히 버틴다.
		/// ★ 그래서 두 겹으로 막는다: ① 스폰 자체를 갈 수 있는 칸으로 스냅(SnapToReachable) ② 그래도 굳으면
		///   가장 가까운 갈 수 있는 칸으로 옮겨준다. 옮긴 사실은 로그로 남긴다 — 조용히 순간이동시키면
		///   다음에 같은 원인이 생겨도 아무도 모른다.
		/// </summary>

		/// <summary>
		/// 아직 자는 서식지 식구인가 — 굳음 감지에서 빼야 한다.
		///
		/// ★ 잠든 마수는 *설계대로* 브레인도 이동도 꺼져 있다. 그런데 굳음 감지기는 둥지 본체만
		///   빼고 식구는 세고 있어서, 판마다 「4초째 못 나아감」 경고를 수십 줄 쏟았다
		///   (실측: 경고 30개 중 21개 · 전부 소속이 찍힌 자는 식구였다). 진짜 굳음이 그 안에 묻힌다.
		/// ★ 깨어난 뒤에는 다시 감지 대상이다 — 그때는 정말로 안 움직이면 사고다.
		/// </summary>
		/// <summary> 「뭉쳐서 못 간다」를 가를 반경 — 이 안의 다른 마수를 센다. </summary>
		private const float STUCK_CROWD_RADIUS = 2.5f;

		private void UnstickEnemies()
		{
			if (mapLayout == null || flowField == null || stageRoot == null)
				return;

			float threshold = stage.StuckRelocateSeconds;
			if (threshold <= 0f)
				return;

			float moveEpsilonSqr = stage.StuckMoveEpsilon * stage.StuckMoveEpsilon;

			foreach (MatchCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (IsNest(enemy))
					continue; // 둥지는 원래 안 움직인다 — 「굳었다」로 세면 매 틱 헛되이 옮기려 든다.
				if (IsSleepingLairMember(enemy))
					continue; // 자는 식구도 마찬가지다 — 아래 참조.

				Vector3 position = enemy.Position;
				if (enemyStillness.TryGetValue(enemy.CombatantId, out (Vector3 Position, float Seconds) tracked) == false)
				{
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}

				if ((position - tracked.Position).sqrMagnitude > moveEpsilonSqr)
				{
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}

				float stillSeconds = tracked.Seconds + TimeManager.TICK;
				if (stillSeconds < threshold)
				{
					enemyStillness[enemy.CombatantId] = (tracked.Position, stillSeconds);
					continue;
				}

				Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(position.ToUnity()).ToSim());
				bool blocked = IsPathBlocked(cell);
				bool reachable = flowField.IsReachable(cell);

				// ★ 「갈 수 있는 자리인데 안 간다」가 경고의 대부분이었다(실측). 그러면 길이 아니라
				//   *그 마수 자신*이 원인이다 — 브레인이 꺼졌거나(목줄이 끄고 안 켰거나 풀에서 꺼진 채
				//   나왔거나), 아직 자는 둥지 식구이거나, 이동 부품이 꺼져 있는 것. 셋은 고치는 자리가
				//   전혀 다른데 지금 로그는 셋을 구별 못 한다 — 추측으로 고치다 한 번 헛짚었다.
				TacticDriver stuckDriver = enemy.GetComponent<TacticDriver>();
				UnitMovement stuckMovement = enemy.GetComponent<UnitMovement>();

				// ★ 「제자리」와 「굳음」은 다르다. 실측에서 굳었다고 찍힌 다섯 중 넷은 *정상 속도로 걷고 있었다* —
				//   앞뒤로 왔다 갔다 해서 4초 전후 위치만 같았을 뿐이다(목줄이 끌고 브레인이 다시 나가는 식).
				//   그걸 「못 나아감」으로 부르면 진짜 막힌 것이 그 안에 묻힌다(예전에 자는 식구를 뺀 것과 같은 이유).
				//   몸이 실제로 나아가고 있으면 굳음이 아니라 왕복이다 — 따로 세고, 경고는 안 쏟는다.
				// ★ 척도를 맞춰야 한다. 처음엔 *한 틱* 이동량(≈0.03)을 *4초짜리* 문턱과 견줘서 왕복이 늘 0 이었다.
				//   옳은 물음은 「이 몸이 자기가 가려던 만큼 실제로 갔나」다 — 가려던 한 틱치의 1/4 이라도
				//   갔으면 막힌 게 아니라 왔다 갔다 하는 것이다.
				float intendedStep = stuckMovement != null ? stuckMovement.Velocity.magnitude * TimeManager.TICK : 0f;
				if (stuckMovement != null && intendedStep > 0f
					&& stuckMovement.LastMoveDelta.magnitude > intendedStep * 0.25f)
				{
					oscillatingCells.Add(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(position.ToUnity()).ToSim()));
					enemyStillness[enemy.CombatantId] = (position, 0f);
					continue;
				}
				TowerDefenseLairMember stuckMember = enemy.GetComponent<TowerDefenseLairMember>();
				// ★ 깨어 있고 이동도 켜졌는데 안 가는 것들이 *붙은 칸에 뭉쳐* 있었다. 서로 밀어 막는
				//   것인지(뭉침) 혼자 굳은 것인지(다른 원인)는 옆에 몇 마리가 있는지로 갈린다.
				int crowd = 0;
				foreach (MatchCombatant other in waveEnemies)
				{
					if (other == null || other == enemy || other.IsAlive == false)
						continue;
					if ((other.Position - position).sqrMagnitude <= STUCK_CROWD_RADIUS * STUCK_CROWD_RADIUS)
						crowd++;
				}

				// ★ 「길찾기가 실패했다」는 판 전체 셈이라, *이 마수가* 길을 못 받는지는 따로 물어야 한다.
				//   같은 자리에서 안내를 직접 요청해 보면 그 하나가 갈린다 — 안내가 나오는데 안 가면
				//   원인은 길이 아니라 이동 쪽이고, 안내 자체가 없으면 길 쪽이다. 지금 로그는 그걸 못 가른다.
				string guide = "안내 물어봄 X";
				if (flowNavigator != null && coreCombatant != null)
				{
					guide = flowNavigator.TryGetSteering(position, coreCombatant.Position, enemy.CombatantId, out Vector3 steer)
						? "안내 있음(" + steer.x.ToString("F2") + "," + steer.z.ToString("F2") + ")"
						: "안내 없음";
				}

				// ★ 마지막 한 겹 — 방향을 *받았는지*와 실제로 *나가는지*는 다르다. 받은 방향이 0 이면
				//   브레인이 명령을 안 준 것이고, 방향은 있는데 속도가 0 이면 몸이 막힌 것이다.
				// ★ 속도만으로는 못 가른다 — 실측에서 *전속(1.60)인데 4초 동안 제자리*인 개체가 나왔다.
				//   속도는 「가려던 값」을 sweep 이 깎은 뒤의 값이라, 0 이 아니어도 몸은 한 발도 못 나갈 수 있다.
				//   그래서 이번 틱이 *실제로 옮긴 거리*와 *벽에 닿은 횟수*를 같이 찍는다:
				//   실제이동 0 + 벽닿음 있음 = 물리 벽에 눌림(격자는 갈 수 있다는데 씬엔 벽이 있다),
				//   실제이동 있음 + 제자리 = 왔다 갔다 하는 왕복, 둘 다 0 = 스스로 안 가는 것.
				// ★ 여태 「받은방향」은 *길이*만 찍었다 — 1.00 이면 방향을 받았다는 뜻일 뿐, 그게 *안내와 같은
				//   방향인지*는 한 번도 안 봤다. 아래 「안내」는 진단이 직접 물어본 값이라, 둘이 다르면
				//   마수는 흐름장을 안 쓰고 딴 데(눈앞의 포탑 등)를 보고 직선으로 걷는 중이다.
				//   길 고치는 것과 목표 고르는 것은 고치는 자리가 전혀 다르다.
				string body = stuckMovement == null ? "이동부품 없음"
					: "받은쪽(" + stuckMovement.MoveDirectionWorld.x.ToString("F2")
						+ "," + stuckMovement.MoveDirectionWorld.z.ToString("F2") + ")"
						+ " · 받은방향 " + stuckMovement.MoveDirectionWorld.magnitude.ToString("F2")
						+ " · 속도 " + stuckMovement.Velocity.magnitude.ToString("F2")
						+ " · 실제이동 " + stuckMovement.LastMoveDelta.magnitude.ToString("F3")
						+ " · 벽닿음 " + stuckMovement.WallContactCount
						// ★ 막은 것의 *이름*이 마지막 갈림길이다 — 지형이면 지도가 틀린 것이고,
						//   다른 마수면 길이 아니라 *길목이 좁아* 밀리는 것이라 고치는 자리가 전혀 다르다.
						+ " · 막은것 " + (stuckMovement.LastWallCollider != null
							? stuckMovement.LastWallCollider.name
							: "없음")
						// ★ 길찾기는 마수를 *점*으로 보고 칸 단위로 답한다. 그런데 몸에는 굵기가 있다 —
						//   몸 지름이 칸보다 크면 「갈 수 있다」는 칸으로도 못 들어가고, 옆 칸 암반에 낀다.
						//   같은 이유로 좁은 길목에서는 서로가 서로의 벽이 된다(정체 119건).
						+ " · 몸반경 " + stuckMovement.BodyRadius.ToString("F2")
						+ " · 칸 " + mapLayout.CellSize.ToString("F2");

				// ★ 「길이 잘못됐나」와 「목표가 잘못됐나」는 고치는 자리가 전혀 다르다 — 길만 네 번 고치다
				//   헛돌았다. 이 마수가 지금 무엇을 향해 가라고 명령받았는지를 같이 찍는다.
				string aim = "목표 없음";
				if (stuckDriver != null && stuckDriver.LastCommandedTarget != null)
				{
					ICombatant aimTarget = stuckDriver.LastCommandedTarget;
					aim = "목표 " + (aimTarget is Component aimComponent ? aimComponent.gameObject.name : aimTarget.GetType().Name)
						+ "(" + Vector3.Distance(aimTarget.Position, enemy.Position).ToString("F1") + ")";
				}

				string why = body + " · " + aim + " · " + guide + " · 브레인 " + (stuckDriver == null ? "없음" : stuckDriver.enabled ? "켜짐" : "꺼짐")
					+ " · 이동 " + (stuckMovement == null ? "없음" : stuckMovement.enabled ? "켜짐" : "꺼짐")
					+ " · 소속 " + (stuckMember != null ? stuckMember.LairId.ToString() : "없음")
					+ " · 옆에 " + crowd + "마리";

				// ★ 밀어주지 않는다 (사용자 지시: "밀어주는게 어딨어... 밀어주는거 제거하세요").
				//   예전엔 굳은 마수를 다음 칸으로 *순간이동*시켜 길을 뚫었다 — 그건 길찾기가 답을 못 준
				//   자리를 손으로 메운 것이고, 벽을 지나가는 것처럼 보이는 원인이기도 했다.
				//   이제 길찾기가 목표가 무엇이든 답하므로, 굳었다는 것은 *진짜 막혔다*는 뜻이다 —
				//   그 사실을 남기기만 하고 판은 마수가 앞을 부수도록 둔다.
				Debug.LogWarning($"{nameof(TowerDefenseMatch)}: 마수가 {stillSeconds:F1}s 째 못 나아감 — cell={cell} "
					+ $"blocked={blocked} reachable={reachable} · {why} (길이 막혔으면 앞을 부순다)");

				// ★ 판마다 값이 크게 흔들려(같은 코드로 경고 99~421) 한 판 비교로는 작은 차이를 못 가른다.
				//   경고 *줄 수*는 같은 마수가 4초마다 다시 찍혀 부풀고, 판이 길수록 늘어난다.
				//   그래서 「몇 *자리*가 굳었나」를 따로 센다 — 이쪽이 판 길이에 덜 흔들린다.
				stuckCells.Add(cell);
				if (stuckMovement != null && stuckMovement.LastWallCollider != null)
				{
					if (stuckMovement.LastWallCollider.GetComponentInParent<MatchCombatant>() != null)
						stuckByUnitCells.Add(cell);
					else
						stuckByTerrainCells.Add(cell);
				}

				enemyStillness[enemy.CombatantId] = (enemy.Position, 0f);
			}
		}

		private readonly HashSet<Vector2Int> stuckCells = new();
		private readonly HashSet<Vector2Int> stuckByTerrainCells = new();
		private readonly HashSet<Vector2Int> stuckByUnitCells = new();
		private readonly HashSet<Vector2Int> oscillatingCells = new();

		/// <summary> 굳은 게 아니라 *왔다 갔다* 한 자리 수 — 몸은 정상 속도로 나아가고 있었다. </summary>
		public int OscillatingCellCount => oscillatingCells.Count;

		/// <summary> 그 칸에서 가장 가까운 「갈 수 있는」 칸 — 나선으로 넓혀 찾는다(없으면 false). </summary>
		private bool TrySnapToReachable(Vector2Int cell, out Vector2Int result)
		{
			result = cell;
			if (flowField.IsReachable(cell))
				return false; // 이미 갈 수 있는 자리 — 굳은 원인이 길이 아니다(옮겨도 소용 없다).

			for (int radius = 1; radius <= stage.StuckSearchRadius; radius++)
			{
				for (int offsetX = -radius; offsetX <= radius; offsetX++)
				{
					for (int offsetY = -radius; offsetY <= radius; offsetY++)
					{
						if (Mathf.Abs(offsetX) != radius && Mathf.Abs(offsetY) != radius)
							continue; // 테두리만 — 안쪽은 이전 반경에서 이미 봤다.

						Vector2Int candidate = new(cell.x + offsetX, cell.y + offsetY);
						if (flowField.IsReachable(candidate) == false)
							continue;

						result = candidate;
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// 죽었거나 풀에 반납된 마수만 목록에서 걷어낸다 — 살아있는 것은 남긴다.
		/// 실시간이라 앞 무리와 상시 마수가 겹쳐 존재하므로, 새 무리를 낼 때 목록을 비우면 안 된다.
		/// </summary>
		private void PruneDeadEnemies()
		{
			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant enemy = waveEnemies[index];
				if (enemy == null || enemy.IsAlive == false)
					waveEnemies.RemoveAt(index);
			}
		}

		/// <summary> 살아있는 웨이브 적 수 — 죽었거나 풀 반환된(null) 엔트리는 조회 겸 정리(멱등). </summary>
		private int CountAliveEnemies()
		{
			int count = 0;
			for (int index = waveEnemies.Count - 1; index >= 0; index--)
			{
				MatchCombatant combatant = waveEnemies[index];
				if (combatant == null)
				{
					waveEnemies.RemoveAt(index);
					continue;
				}
				if (combatant.IsAlive)
					count++;
			}
			return count;
		}

		private void ReleaseSoldUnit(GameObject sold)
		{
			MatchCombatant combatant = sold.GetComponent<MatchCombatant>();
			if (combatant != null && targeting != null)
			{
				targeting.Unregister(combatant);
				registeredCombatants.Remove(combatant);
			}

			TacticDriver driver = sold.GetComponent<TacticDriver>();
			if (driver != null)
				driver.StopDriving();

			supplyChain.Remove(sold.transform);
			// 판 것은 더 이상 전기를 먹지 않는다 — 안 지우면 없는 건물이 계속 전력을 물고 있어
			// 「분명 발전기를 지었는데 왜 모자라지」가 된다(무음 누수).
			powerGrid.RemoveConsumer(sold.transform);
			harvesterIsOuter.Remove(sold.transform);
			ReleaseUnit(pool, sold);
			spawnedUnits.Remove(sold);
			RefreshSupply(); // 사슬 중간이 사라지면 그 너머가 통째로 끊긴다.
			RefreshPower();  // 먹는 입이 줄었으니 누가 다시 돌아가는지도 즉시 반영.
		}

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
