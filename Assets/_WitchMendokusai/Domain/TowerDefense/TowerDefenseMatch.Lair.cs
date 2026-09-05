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
	// TowerDefenseMatch 의 서식지 배치 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
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
		public float LairWakeRadius => stage != null ? stage.LairWakeRadius : 0f;
	}
}
