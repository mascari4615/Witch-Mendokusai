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
	// TowerDefenseMatch 의 마수 둥지 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
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
	}
}
