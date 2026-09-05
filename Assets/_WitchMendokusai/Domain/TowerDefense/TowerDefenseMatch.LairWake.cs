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
	// TowerDefenseMatch 의 서식지 깨우기와 끈 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
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
