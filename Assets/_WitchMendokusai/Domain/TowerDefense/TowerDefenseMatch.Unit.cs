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
	// TowerDefenseMatch 의 유닛 손질과 반납 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
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
	}
}
