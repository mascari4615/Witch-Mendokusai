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
	// TowerDefenseMatch 의 Lair 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		/// <summary>
		/// 마수 출현 표시 — 어디서 적이 들어오는지 모르면 방어선을 세울 수가 없다.
		/// 사용자 실증: 자원 노드 원을 "몬스터 나오는 원" 으로 오인했다. 원인은 ① 출현 지점에 아무
		/// 표시가 없었고 ② 자원 노드가 출현선 바로 앞(z=14 vs 출현 z=15)에 깔려 있어서 — 즉
		/// *표시 부재* + *배치 오류* 가 겹쳤다. 출현 지점에 붉은 표식을 세워 둘을 확실히 가른다.
		/// 노드(금빛 원반)와 형태·색을 다르게 해야 혼동이 안 난다.
		/// </summary>
		// ── 마수 둥지(출현지) ─────────────────────────────────────────────────────
		// ★ 왜 부술 수 있어야 하나 (사용자 지시: "적유닛이 나오는 곳도 뭔가 부술 수 있거나 나오는 적이
		//   한정되어야 할듯"): 무한히 쏟아지는 출구를 못 막으면 방어는 영원히 수세다. 둥지를 부수면
		//   그쪽 출구가 닫힌다 — 「버틴다」에서 「밀어낸다」로 게임의 동사가 하나 늘어난다.
		// ★ 왜 마수 프리팹으로 세우나: 포탑은 *마수 목록에 있는 것*만 쏜다. 둥지를 같은 종류로 세우면
		//   조준·피해·격파 보상 경로를 하나도 새로 만들지 않고 그대로 재사용한다.
		private readonly List<(MatchCombatant Combatant, Vector3 LocalPosition)> nests = new();
		// 둥지인지 즉시 알기 위한 집합 — 「쏠 대상」과 「쳐들어오는 마수」를 가르는 기준.
		private readonly HashSet<MatchCombatant> nestCombatants = new();
		private bool nestsEverSpawned; // 처음부터 둥지가 없던 판(옛 방식)을 「전멸」로 오인하지 않게.

		// 이 판에서 부순 둥지 자리 — 이어할 때 그 자리엔 다시 안 선다.
		private readonly List<Vector3> destroyedNestPositions = new();

		private bool IsNestAlreadyDestroyed(Vector3 localPosition)
		{
			foreach (Vector3 destroyed in destroyedNestPositions)
			{
				if ((destroyed - localPosition).sqrMagnitude <= 1f)
					return true;
			}
			return false;
		}

		private IEnumerator SpawnNestsRoutine()
		{
			if (stage.NestHealthMultiplier <= 0f || stage.EnemyUnit == null || stage.EnemyUnit.Prefab == null)
				yield break;

			foreach (Vector3 localPosition in new List<Vector3>(activeSpawnPoints))
			{
				// ★ 이미 부순 둥지는 다시 서지 않는다 — 안 그러면 이어할 때마다 부순 것이 되살아나
				//   「부술 수 있다」가 헛수고가 된다(부순 자리는 저장에 적혀 있다).
				if (IsNestAlreadyDestroyed(localPosition))
					continue;

				SpawnedUnit spawned = new();
				yield return SpawnUnitRoutine(stage.EnemyUnit, stageRoot.TransformPoint(localPosition.ToUnity()).ToSim(),
					ATTACKER_TEAM, stage.NestTint, stage.NestScale, spawned);
				if (spawned.Ok == false)
					continue;

				GameObject nestObject = spawned.GameObject;
				UnitObject nestUnit = spawned.UnitObject;
				MatchCombatant nestCombatant = spawned.Combatant;

				yield return null;
				if (core == null)
					yield break;

				// 둥지는 걷지 않는다 — 이동을 끄고 자리에 못 박는다(브레인은 세우는 문이 이미 껐다).
				UnityEngine.AI.NavMeshAgent nestAgent = nestObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
				if (nestAgent != null)
					nestAgent.enabled = false;
				UnitMovement nestMovement = nestObject.GetComponent<UnitMovement>();
				if (nestMovement != null)
					nestMovement.enabled = false;

				int nestHp = Mathf.Max(1, Mathf.RoundToInt(
					nestUnit.UnitStat[UnitStatType.HP_MAX] * stage.NestHealthMultiplier * difficulty.NestHealthScale));
				nestUnit.UnitStat[UnitStatType.HP_MAX] = nestHp;
				nestUnit.UnitStat[UnitStatType.HP_CUR] = nestHp;

				// 표적 등록은 세우는 문이 이미 했다 — 여기서 또 하면 같은 것이 목록에 두 번 들어간다.
				waveEnemies.Add(nestCombatant); // 포탑이 쏘는 대상 목록 — 둥지도 여기 있어야 맞는다.
				nests.Add((nestCombatant, localPosition));
				nestCombatants.Add(nestCombatant);
			}

			nestsEverSpawned = nests.Count > 0;
			Debug.Log($"{nameof(TowerDefenseMatch)}: 마수 둥지 {nests.Count}곳 — 전부 부수면 개척 성공.");
		}

		// 잠들어 있는 서식지 마수 — 깨어나기 전까지는 걷지도 때리지도 않는다.
		/// <summary> 서식지 번호 발급기 — 판이 새로 시작돼도 옛 번호와 안 겹치게 계속 는다. </summary>
		private int lastLairId;

		private sealed class SleepingLair
		{
			public int Id;
			public Vector3 WorldPosition;
			public readonly List<UnitObject> Guards = new();
			public readonly List<TacticDriver> Drivers = new();
			public bool Awake;
			public bool NoiseWarned; // 「여기 소리가 크다」는 한 곳당 한 번만.
			public bool Cleared; // 보상은 한 번만 — 안 그러면 빈 서식지가 매 프레임 정수를 찍어낸다.
		}

		/// <summary>
		/// 서식지 하나를 강제로 깨운다(검증 전용) — 깨어난 마수가 *어디로 가는지*는 깨워봐야 잴 수 있다.
		/// 깨운 서식지의 자리를 돌려준다(못 깨웠으면 false).
		/// </summary>
		public bool WakeNearestLairForVerification(out Vector3 lairPosition)
		{
			lairPosition = Vector3.zero;

			// ★ 이름이 「가장 가까운」인데 실제로는 *목록의 첫 번째*를 깨우고 있었다. 그래서 판 반대편
			//   서식지가 뽑혀 「코어까지 102」 같은 값이 나왔고, 그걸 근거로 「서식지가 너무 멀다」고
			//   의심했다 — 실제 가장 가까운 것은 16 이었다. **이름이 거짓말하면 측정이 거짓말한다.**
			SleepingLair best = null;
			float bestDistance = float.MaxValue;
			Vector3 from = coreCombatant != null ? coreCombatant.Position : Vector3.zero;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake)
					continue;
				float distance = Vector3.Distance(lair.WorldPosition, from);
				if (distance >= bestDistance)
					continue;
				bestDistance = distance;
				best = lair;
			}

			if (best == null)
				return false;

			lairPosition = best.WorldPosition;
			WakeLair(best);
			return true;
		}

		/// <summary>
		/// 깨어난 서식지 마수들이 지금 코어에서 얼마나 떨어져 있나(평균). 시간에 따라 이 값이 줄면
		/// 「코어로 행진한다」, 제자리면 「그 일대를 지킨다」 — 둘은 완전히 다른 게임이다.
		/// </summary>
		public float AwakenedGuardDistanceToCore() => AwakenedGuardDistanceToCore(out _);

		/// <summary> 같은 값 + *몇 기를 재고 있나*. 0 기면 「가까워졌다」가 아니라 「죽어서 없다」다. </summary>
		public float AwakenedGuardDistanceToCore(out int aliveGuards) => AwakenedGuardDistanceToCore(out aliveGuards, out _, out _);

		/// <summary>
		/// 같은 값 + **사라진 방식**까지. 「참조가 비었다(파괴)」와 「꺼져 있다(풀 반납)」는 원인이 전혀 다르다 —
		/// 숫자 하나만 보면 둘이 똑같이 「없다」로 보여서 엉뚱한 데를 파게 된다.
		/// </summary>
		public float AwakenedGuardDistanceToCore(out int aliveGuards, out int destroyedGuards, out int disabledGuards)
		{
			aliveGuards = 0;
			destroyedGuards = 0;
			disabledGuards = 0;
			if (coreCombatant == null)
				return -1f;

			float total = 0f;
			int count = 0;
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard == null)
					{
						destroyedGuards++;
						continue;
					}
					if (guard.gameObject.activeInHierarchy == false)
					{
						disabledGuards++;
						continue;
					}
					total += Vector3.Distance(guard.transform.position.ToSim(), coreCombatant.Position);
					count++;
				}
			}
			aliveGuards = count;
			return count > 0 ? total / count : -1f;
		}

		/// <summary>
		/// 깨어난 마수가 *제 서식지에서* 얼마나 멀어졌나(최대). 「지킨다」의 진짜 판정은 이것이다 —
		/// 코어까지의 거리로 재면 서식지가 원래 코어에 가까웠는지 멀었는지에 답이 좌우된다.
		/// </summary>
		public float AwakenedGuardDistanceFromHome()
		{
			float worst = -1f;
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard == null || guard.gameObject.activeInHierarchy == false)
						continue;
					float distance = Vector3.Distance(guard.transform.position.ToSim(), lair.WorldPosition);
					if (distance > worst)
						worst = distance;
				}
			}
			return worst;
		}

		/// <summary> 미니맵이 읽는 서식지 표식 — 자리와 「깨어났나」. </summary>
		public readonly struct LairMarker
		{
			public readonly Vector3 Position;
			public readonly bool Awake;

			public LairMarker(Vector3 position, bool awake)
			{
				Position = position;
				Awake = awake;
			}
		}

		private readonly List<LairMarker> lairMarkers = new();

		/// <summary>
		/// 서식지 자리 목록. **밝힌 것만 그리는 판단은 화면이 한다**(시야 규칙은 화면 공통).
		///
		/// ★ 왜 따로 내주나: 잠든 마수도 마수 목록에 있어서 미니맵이 「코어로 오는 중」이라는 *거짓말*을
		///   붙이고 있었다. 잠든 무리와 몰려오는 무리는 대응이 정반대다(피한다 / 막는다) —
		///   같은 점으로 그리면 「깨울지 말지」를 계산할 수가 없다.
		/// </summary>
		public IReadOnlyList<LairMarker> LairMarkers
		{
			get
			{
				lairMarkers.Clear();
				foreach (SleepingLair lair in lairs)
					lairMarkers.Add(new LairMarker(lair.WorldPosition, lair.Awake));
				return lairMarkers;
			}
		}

		/// <summary> 그 마수가 아직 잠든 서식지 소속인가 — 미니맵이 마수 점에서 걸러낸다. </summary>
		public bool IsSleepingLairGuard(MatchCombatant combatant)
		{
			if (combatant == null)
				return false;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null && guard.gameObject == combatant.gameObject)
						return true;
				}
			}
			return false;
		}

		/// <summary> 깨어난 서식지 소속인가 — 목줄이 그 전술을 잠시 끌 수 있어 굳음 판정에서 뺀다. </summary>
		private bool IsAwakenedLairGuard(MatchCombatant combatant)
		{
			if (combatant == null)
				return false;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null && guard.gameObject == combatant.gameObject)
						return true;
				}
			}
			return false;
		}
		public int SleepingLairCount => lairs.Count;

		private readonly List<SleepingLair> lairs = new();
		private readonly List<Vector3> lairWakeProbe = new();

		/// <summary> 지금까지 깨운 서식지 수 — 결과 기록판이 「얼마나 파고들었나」를 말한다. </summary>
		public int LairsAwakened { get; private set; }

		/// <summary> 그중 *소리만으로* 깨어난 수 — 거리로 깬 것과 갈라야 소리 규칙을 잴 수 있다. </summary>
		public int LairsAwakenedByNoise { get; private set; }

		/// <summary>
		/// 판 곳곳에 잠든 마수를 깐다 (TASK-WM-194, 데아빌 레퍼런스).
		///
		/// ★ 파도만 있으면 파도 사이의 판 안쪽은 완전히 안전해서, 넓히기를 미루는 데 아무 대가가 없다.
		///   잠든 것이 깔려 있으면 **넓히는 행위 자체가 위험**이 된다 = 「개척」이 성립한다.
		/// </summary>
		private IEnumerator SpawnLairsRoutine()
		{
			lairs.Clear();
			LairsAwakened = 0;
			LairsCleared = 0;
			// ★ 「바뀔 때만 알린다」용 기억도 판마다 비운다. 안 비우면 새 판이 옛 판의 상태를 이어받아
			//   *같은 일이 처음 일어나도 알리지 않는다*(적응) 또는 *한참 뒤까지 안 알린다*(강도).
			lastAdaptationNote = string.Empty;
			lastPressureStep = -1;

			// ★ 알림과 「건물이 서 있던 자리」 기억도 판마다 비운다.
			//   안 비우면 새 판 첫 틱에 *옛 판 건물들*이 전부 「내 것이 부서졌다」로 뜬다 —
			//   판이 끝나며 청산된 것을 적이 부순 것으로 오인한다(시작하자마자 거짓 경고 넷).
			alerts.Clear();
			lastBuildingPositions.Clear();
			breach.Clear(); // 새 판은 새 판이다 — 지난 판에서 뚫린 자리가 방향을 끌면 안 된다.
			noise.Clear(); // 지난 판의 소리가 새 판의 둥지를 깨우면 안 된다(판 넘는 상태를 여기서 세 번 잡았다).

			if (stage == null || stage.LairCount <= 0 || mapLayout == null || stage.EnemyUnit == null)
				yield break;

			List<Vector2Int> cells = new();
			TowerDefenseLairPlacement.Choose(
				mapLayout.Seed,
				mapLayout.Width,
				mapLayout.Length,
				mapLayout.CoreCell,
				cell => mapLayout.IsBlocked(cell),
				stage.LairCount,
				stage.LairMinCoreDistance,
				stage.LairMinSpacing,
				cells);

			foreach (Vector2Int cell in cells)
			{
				Vector3 localPosition = mapLayout.CellToWorld(cell);
				// 번호는 판 안에서만 유일하면 된다 — 소속 표가 이 번호로 「내 집인가」를 가른다.
				SleepingLair lair = new()
				{
					Id = ++lastLairId,
					WorldPosition = stageRoot.TransformPoint(localPosition.ToUnity()).ToSim(),
				};

				for (int guard = 0; guard < stage.LairGuardCount; guard++)
				{
					// 한 자리에 겹쳐 세우면 서로의 몸에 끼어 못 나온다 — 둘레로 조금씩 벌린다.
					float angle = guard * Mathf.PI * 2f / Mathf.Max(1, stage.LairGuardCount);
					Vector3 offset = new(Mathf.Cos(angle) * stage.EnemySpawnSpread, 0f, Mathf.Sin(angle) * stage.EnemySpawnSpread);

					// ★ 둘레로 벌린 그 자리가 암반일 수 있다 — 그러면 그 식구는 깨어나도 못 걷고
					//   판이 끝날 때까지 바위 안에서 민다(실측: 마지막까지 남은 굳음 1곳이 정확히 이것이었다).
					//   파도 마수는 태어날 때 걸을 수 있는 칸으로 밀어주는데 식구에겐 그 장치가 없었다.
					Vector3 guardLocal = localPosition + offset;
					if (mapLayout != null)
					{
						Vector2Int guardCell = mapLayout.WorldToCell(guardLocal);
						if (IsPathBlocked(guardCell) && TrySnapToReachable(guardCell, out Vector2Int freeCell))
							guardLocal = mapLayout.CellToWorld(freeCell);
					}

					SpawnedUnit spawned = new();
					yield return SpawnUnitRoutine(stage.EnemyUnit, stageRoot.TransformPoint(guardLocal.ToUnity()).ToSim(),
						ATTACKER_TEAM, stage.LairSleepTint, stage.EnemyScale, spawned);
					if (spawned.Ok == false)
						continue;

					yield return null;
					if (core == null || targeting == null || pool == null)
						yield break;

					// 잠든 동안은 걷지 않는다 — 브레인은 세우는 문이 이미 껐고, 여기서 이동만 못 박는다.
					UnitMovement movement = spawned.GameObject.GetComponent<UnitMovement>();
					if (movement != null)
						movement.enabled = false;

					IgnoreHeroCollision(spawned.GameObject);
					lair.Guards.Add(spawned.UnitObject);
					// 소속을 *몸에* 붙인다 — 목록만으로는 풀에서 되살아난 남의 몸을 못 가른다.
					TowerDefenseLairMember member = spawned.GameObject.GetComponent<TowerDefenseLairMember>();
					if (member == null)
						member = spawned.GameObject.AddComponent<TowerDefenseLairMember>();
					member.Join(lair.Id);
					waveEnemies.Add(spawned.Combatant); // 포탑이 쏘는 대상 — 잠들었어도 때릴 수는 있다.
					enemyBountyById[spawned.Combatant.CombatantId] = core.BountyPerKill;
				}

				if (lair.Guards.Count > 0)
					lairs.Add(lair);
			}

			Debug.Log($"{nameof(TowerDefenseMatch)}: 서식지 {lairs.Count}곳이 잠들어 있다 — 가까이 가면 깨어난다.");
		}

		/// <summary>
		/// 내 것이 가까이 왔으면 서식지를 깨운다. 깨어난 마수는 보통 마수와 똑같이 움직인다.
		///
		/// ★ 「가까이 가면 깬다」여야 넓히는 것이 위험이 된다 — 처음부터 다 깨어 있으면 파도가 하나 더
		///   있는 것이고, 영영 안 깨면 판을 장식하는 조형물이다.
		/// </summary>
		private void WakeNearbyLairs()
		{
			if (lairs.Count == 0 || stage == null || stage.LairWakeRadius <= 0f)
				return;

			lairWakeProbe.Clear();
			foreach (Transform building in supplyChain.Buildings)
			{
				if (building != null)
					lairWakeProbe.Add(building.position.ToSim());
			}
			if (heroTransform != null)
				lairWakeProbe.Add(heroTransform.position.ToSim()); // 영웅 정찰도 건드림

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake)
					continue;

				bool tooClose = TowerDefenseLairPlacement.ShouldWake(
					lair.WorldPosition, lairWakeProbe, stage.LairWakeRadius);

				// ★ 거리만 보면 「멀찍이서 조용히 크는 것」과 「바로 옆에서 난사하는 것」이 똑같이
				//   안전하다 — 개척의 위험이 거리 하나로 납작해진다. 소리도 깨운다:
				//   짓고, 쏘고, 얻어맞는 소리가 마수를 부른다(데아빌의 축은 거리가 아니라 내 행동이다).
				float heard = stage.NoiseWakeThreshold > 0f
					? noise.LevelAt(lair.WorldPosition, stage.NoiseHearingRadius)
					: 0f;
				bool tooLoud = stage.NoiseWakeThreshold > 0f && heard >= stage.NoiseWakeThreshold;

				// ★ 깨어난 뒤에 알리면 대응할 기회가 0 이다 — 이미 벌어진 일을 통보받을 뿐이다.
				//   문턱에 다가가는 동안 한 번 말해 줘야 「그만 쏠까 · 물러설까」가 결정이 된다.
				//   한 곳당 한 번만(매 프레임 외치면 다른 알림을 덮는다).
				if (tooLoud == false && lair.NoiseWarned == false && stage.NoiseWarnFraction > 0f
					&& heard >= stage.NoiseWakeThreshold * stage.NoiseWarnFraction)
				{
					lair.NoiseWarned = true;
					NoiseWarnings++;
					alerts.Raise("여기 소리가 크다", lair.WorldPosition, Time.time, stage.AlertSeconds);
				}

				if (tooClose == false && tooLoud == false)
					continue;

				// ★ 「소리 때문」과 「가까이 갔기 때문」은 사람에게 다른 사건이다. 가까이 간 건 스스로
				//   아는데(내가 걸어갔다), 소리는 *멀리서* 일어난 일이라 말해 주지 않으면 이유를 모른다.
				//   그래서 소리만으로 깬 경우에만 알린다 — 그리고 그 수를 따로 센다.
				//   둘을 안 세면 검사가 「소리로 깼나 거리로 깼나」를 영영 못 가른다(실측에서 막혔다).
				bool byNoise = tooLoud && tooClose == false;
				if (byNoise)
					LairsAwakenedByNoise++;

				WakeLair(lair, byNoise);
			}
		}

		/// <summary>
		/// 깨어난 서식지 마수를 제 자리에 묶어 둔다.
		///
		/// ★ 실측으로 잡았다: 깨우면 8초에 코어 쪽으로 58 만큼 다가갔다(101 → 43). 그러면 서식지는
		///   「파도 하나 더」일 뿐이고, 「넓히는 행위 자체가 위험」이라는 이 기능의 존재 이유가 사라진다.
		///   *그 자리를 지켜야* 「저기 자는 걸 깨우면 저기가 위험해진다」가 성립한다.
		/// ★ 목줄 밖에서는 전술을 잠시 끄고 집으로 몬다 — 켜둔 채 방향만 덮어쓰면 같은 프레임에
		///   전술이 다시 코어를 겨눠 서로 밀치며 덜덜 떤다(어느 쪽이 나중에 도는지에 결과가 달림).
		/// </summary>
		private void TickLairLeash()
		{
			if (stage == null || stage.LairLeashRadius <= 0f || lairs.Count == 0)
				return;

			float leash = stage.LairLeashRadius;
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false)
					continue;

				for (int index = lair.Guards.Count - 1; index >= 0; index--)
				{
					UnitObject guard = lair.Guards[index];
					if (guard == null)
					{
						lair.Guards.RemoveAt(index);
						continue;
					}

					// ★ 죽은 마수의 몸은 풀로 돌아가 *다른 곳에서 다른 마수로* 되살아난다. 그런데 이 목록이
					//   그 몸을 계속 들고 있으면, 테두리에서 막 나온 파도 마수를 이 서식지가 집으로 끌어당긴다
					//   — 실측에서 「집에서 123 (목줄 20)」이 그것이었다. 죽는 순간 목록에서 뺀다.
					MatchCombatant combatant = guard.GetComponent<MatchCombatant>();
					if (combatant == null || combatant.IsAlive == false)
					{
						lair.Guards.RemoveAt(index);
						continue;
					}

					// ★ 살아 있다고 내 식구인 것은 아니다 — 죽었다가 풀에서 *다른 마수로* 되살아난
					//   몸은 멀쩡히 살아 있다. 소속 표를 봐야 가른다(「죽었으면 뺀다」로는 못 막았다).
					TowerDefenseLairMember member = guard.GetComponent<TowerDefenseLairMember>();
					if (member == null || member.LairId != lair.Id)
					{
						lair.Guards.RemoveAt(index);
						continue;
					}

					if (guard.gameObject.activeInHierarchy == false)
						continue;

					Vector3 toHome = lair.WorldPosition - guard.transform.position.ToSim();
					bool tooFar = toHome.sqrMagnitude > leash * leash;

					TacticDriver driver = guard.GetComponent<TacticDriver>();
					if (driver != null && driver.enabled == tooFar)
						driver.enabled = tooFar == false;

					if (tooFar == false)
						continue;

					UnitMovement movement = guard.GetComponent<UnitMovement>();
					if (movement != null)
						movement.SetMoveDirection(toHome.normalized.ToUnity());
				}
			}
		}

		/// <summary>
		/// 다 쓸어낸 서식지에 보상을 준다.
		///
		/// ★ 왜 필요한가: 정수는 「바깥 노드까지 나가서 캐는 것」 하나에만 묶여 있었고, 그 길이 막히면
		///   강화가 통째로 잠긴다(사용자 실증: "초반에 연구 어떻게 하라는 겁니까"). 둥지를 부수는 길이
		///   이미 그 짝으로 있으므로, 서식지 소탕도 같은 자리에 둔다 —
		///   **캐서 버는 길과 싸워서 버는 길이 갈라져야** 어느 한쪽이 막혀도 판이 안 죽는다.
		/// ★ 깨운 적 없는 서식지는 세지 않는다. 안 그러면 판이 시작하자마자 「빈 서식지」로 오인될 수 있다.
		/// </summary>
		private void CollectClearedLairs()
		{
			if (stage == null || stage.LairClearEssenceReward <= 0 || core == null)
				return;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Cleared || lair.Awake == false)
					continue;

				bool anyAlive = false;
				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null && guard.gameObject.activeInHierarchy)
					{
						anyAlive = true;
						break;
					}
				}
				if (anyAlive)
					continue;

				lair.Cleared = true;
				LairsCleared++;
				int reward = Mathf.Max(0, Mathf.RoundToInt(stage.LairClearEssenceReward * boons.EssenceMultiplier));
				core.AddEssence(reward);
				PopWorldText("정수 +" + reward, lair.WorldPosition, TextType.Exp);
				alerts.Raise("서식지를 쓸었다", lair.WorldPosition, Time.time, stage.AlertSeconds);
				Debug.Log($"{nameof(TowerDefenseMatch)}: 서식지 소탕 — 정수 +{reward} (지금까지 {LairsCleared}곳).");
			}
		}

		/// <summary>
		/// 깨어난 서식지 한 곳을 쓸어낸 것으로 만든다(검증 전용) — 보상 경로는 *다 죽어야* 밟히는데,
		/// 하네스가 전투로 그걸 만들기는 어렵다. 규칙이 보는 조건(살아있는 게 없다)을 그대로 만들어
		/// 「다 쓸면 정수가 나오는가」만 확인한다.
		/// </summary>
		public bool ClearAwakenedLairForVerification()
		{
			foreach (SleepingLair lair in lairs)
			{
				if (lair.Awake == false || lair.Cleared)
					continue;

				foreach (UnitObject guard in lair.Guards)
				{
					if (guard != null)
						guard.gameObject.SetActive(false);
				}
				return true;
			}
			return false;
		}

		/// <summary> 쓸어낸 서식지 수 — 결과 기록판이 「얼마나 밀어냈나」를 말한다. </summary>
		public int LairsCleared { get; private set; }

		/// <param name="byNoise">
		/// 소리만으로 깼는가. ★ 한 사건에는 알림 하나여야 한다 — 예전엔 「소리를 듣고 깨어났다」를
		/// 띄운 직후 여기서 「서식지가 깨어났다」를 또 띄웠고, 둘이 같은 자리라 합쳐지면서
		/// *뒤엣것이 앞엣것을 덮었다*. 이유를 말하려고 띄운 문구가 조용히 사라진 것이다(실측으로 잡음).
		/// </param>
		private void WakeLair(SleepingLair lair, bool byNoise = false)
		{
			lair.Awake = true;
			LairsAwakened++;

			foreach (UnitObject guard in lair.Guards)
			{
				if (guard == null)
					continue;

				UnitMovement movement = guard.GetComponent<UnitMovement>();
				if (movement != null)
					movement.enabled = true;

				foreach (Renderer guardRenderer in guard.GetComponentsInChildren<Renderer>(true))
					guardRenderer.material.color = stage.EnemyTint; // 잠든 색을 벗는다 — 깨어난 것이 보여야 한다.

				TacticDriver driver = guard.GetComponent<TacticDriver>();
				if (driver == null)
					driver = guard.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(stage.EnemyTactic, targeting, timeManager);
				driver.Navigator = flowNavigator;
				driver.StopsToAttack = false;
				drivers.Add(driver);
				lair.Drivers.Add(driver);
			}

			PopWorldText("깨어났다", lair.WorldPosition, TextType.Warning);
			alerts.Raise(byNoise ? "소리를 듣고 깨어났다" : "서식지가 깨어났다",
				lair.WorldPosition, Time.time, stage.AlertSeconds);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 서식지 하나가 깨어났다 — 지금까지 {LairsAwakened}곳.");
		}

		/// <summary> 부서진 둥지의 출구를 닫는다 — 그 자리에서 더는 마수가 안 나온다. </summary>
		private void CullDestroyedNests()
		{
			for (int index = nests.Count - 1; index >= 0; index--)
			{
				(MatchCombatant combatant, Vector3 localPosition) = nests[index];
				if (combatant != null && combatant.IsAlive)
					continue;

				nests.RemoveAt(index);
				NestsDestroyed++;
				destroyedNestPositions.Add(localPosition); // 저장이 「어디를 부쉈나」를 적을 수 있게.

				// ★ 정수가 「바깥 채집」 하나에만 묶여 있으면 그 길이 막히는 순간 강화 전체가 잠긴다
				//   (이 작업에서 두 번 겪었다). 둥지를 부수는 것도 정수가 나오는 길이다 —
				//   *캐서 버는 길*과 *싸워서 버는 길*이 갈라지면 어느 한쪽이 막혀도 판이 안 죽는다.
				if (core != null && stage.NestEssenceReward > 0)
				{
					// ★ 「정수 수급」 카드를 여기 태운다. 카드는 뽑히는데 **걸리는 자리가 한 군데도 없어서**
					//   화면엔 「정수↑」라 적히고 실제로는 한 톨도 더 안 들어왔다(뽑으면 그 선택이 버려진다).
					core.AddEssence(Mathf.Max(0, Mathf.RoundToInt(stage.NestEssenceReward * boons.EssenceMultiplier)));
					PopWorldText("정수 +" + stage.NestEssenceReward, stageRoot.TransformPoint(localPosition.ToUnity()).ToSim(), TextType.Exp);
				}
				activeSpawnPoints.Remove(localPosition);
				PopWorldText("둥지 파괴", stageRoot.TransformPoint(localPosition.ToUnity()).ToSim(), TextType.Heal);
				Debug.Log($"{nameof(TowerDefenseMatch)}: 둥지 하나가 무너졌다 — 남은 출구 {activeSpawnPoints.Count}곳.");

				// ★ 마지막 둥지가 무너지면 이긴다 — 실시간 전환으로 「N웨이브를 넘기면 승리」가 사라진 뒤
				//   유일하게 남은 *끝*이다. 끝이 없으면 아무리 잘해도 언젠가 지는 게임이 되고,
				//   그건 「밀어낸다」를 넣은 의미를 통째로 없앤다.
				if (nests.Count == 0 && nestsEverSpawned)
				{
					Debug.Log($"{nameof(TowerDefenseMatch)}: 마지막 둥지가 무너졌다 — 개척 성공.");
					Conclude(TowerDefenseOutcome.Victory);
				}
			}
		}

		private bool IsNest(MatchCombatant combatant)
		{
			foreach ((MatchCombatant nest, Vector3 _) in nests)
			{
				if (nest == combatant)
					return true;
			}
			return false;
		}

		/// <summary> 남은 마수 출구 수 — 화면이 「얼마나 밀어냈나」를 말한다. </summary>
		public int NestCount => nests.Count;

		/// <summary> 둥지 자리들 — 미니맵이 마수와 갈라 크게 그린다. </summary>
		public IEnumerable<Vector3> NestPositions
		{
			get
			{
				foreach ((MatchCombatant nest, Vector3 _) in nests)
				{
					if (nest != null && nest.IsAlive)
						yield return nest.Position;
				}
			}
		}

		/// <summary> 그 마수가 둥지인가 — 화면이 둘을 다르게 그린다. </summary>
		public bool IsNestCombatant(MatchCombatant combatant) => IsNest(combatant);
		public int NestsDestroyed { get; private set; }
		public float LairWakeRadius => stage != null ? stage.LairWakeRadius : 0f;

		private bool IsSleepingLairMember(MatchCombatant enemy)
		{
			TowerDefenseLairMember member = enemy.GetComponent<TowerDefenseLairMember>();
			if (member == null || member.LairId < 0)
				return false;

			foreach (SleepingLair lair in lairs)
			{
				if (lair.Id != member.LairId)
					continue;
				return lair.Awake == false;
			}
			return false;
		}
	}
}
