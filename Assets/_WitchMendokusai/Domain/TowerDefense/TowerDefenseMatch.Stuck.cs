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
	// TowerDefenseMatch 의 막힌 적 풀기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
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
	}
}
