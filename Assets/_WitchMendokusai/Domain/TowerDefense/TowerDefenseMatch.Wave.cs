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
	// TowerDefenseMatch 의 물결 구성과 침공 방향 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 이번 파도가 밀려오는 테두리 토막(무대 로컬). 파도마다 다시 뽑히므로 출구가 고정되지 않는다
		// = 「길」이 안 생긴다. 비어 있으면 옛 고정 둥지 방식으로 되돌아간다.
		private readonly List<Vector3> invasionFront = new();

		// 이번(또는 다음) 웨이브의 마수 구성 — 원소 = EnemyArchetypes 인덱스. 결정론이라 화면 예고와 실제 스폰이 같다.
		private readonly List<int> waveComposition = new();

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
		public int InvasionFrontCount => invasionFront.Count;

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
	}
}
