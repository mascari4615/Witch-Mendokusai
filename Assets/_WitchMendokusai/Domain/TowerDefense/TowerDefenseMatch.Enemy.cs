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
	// TowerDefenseMatch 의 적 추적과 정리 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 매 틱 aliveEnemies 카운트용 — 죽거나 풀 반환된(null) 엔트리는 조회 시 제거(멱등 정리).
		// 웨이브마다 SpawnWaveRoutine 시작에서 비움(이전 웨이브 잔여가 다음 웨이브에 누적되는 것 방지).
		private readonly List<MatchCombatant> waveEnemies = new();

		// 격파 보상을 이미 지급한 마수(CombatantId) — 죽은 개체는 여러 틱 동안 목록에 남으므로 중복 지급 차단.
		// 오브젝트 풀이 같은 GameObject 를 되돌려주기 때문에 참조가 아니라 매치 고유 id 로 센다.
		private readonly HashSet<int> bountyPaidEnemyIds = new();

		// 격파 보상은 종류마다 다르다(단단한 놈일수록 크게) — 죽은 뒤엔 어떤 종류였는지 알 수 없으므로
		// 스폰 시점에 CombatantId → 보상액을 기록해 둔다.
		private readonly Dictionary<int, int> enemyBountyById = new();

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
		public int LeakedCount { get; private set; }

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
	}
}
