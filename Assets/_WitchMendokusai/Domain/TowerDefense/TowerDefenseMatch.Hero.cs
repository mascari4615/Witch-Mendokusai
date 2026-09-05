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
	// TowerDefenseMatch 의 Hero 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch
	{
		// 영웅 인형 — 유일하게 *움직이는* 내 편. 포탑과 같은 전투 표를 쓰되 자리를 내가 옮긴다.
		private Transform heroTransform;
		// 영웅을 실제로 걷게 하는 부품 — 좌표를 직접 옮기지 않는 이유는 세우는 곳의 ★ 주석 참고.
		private UnitMovement heroMovement;
		private MatchCombatant heroCombatant;
		private Vector3 heroTargetPosition;
		private bool heroActive;

		/// <summary> 이 마수와 영웅의 몸싸움을 무시시킨다 — 영웅이 이미 서 있을 때 태어난 마수용. </summary>
		private void IgnoreHeroCollision(GameObject enemy)
		{
			if (enemy == null || heroTransform == null)
				return;
			IgnorePair(heroTransform.gameObject, enemy);
		}

		// ── 영웅 인형 ─────────────────────────────────────────────────────────────
		// ★ 왜 필요한가: 지금 개척은 「전부 미리 배치하고 지켜본다」라 교전 중에 사람이 할 일이 0 이다.
		//   움직이는 내 편이 하나 있으면 「부족한 곳을 내가 뛰어가 메운다」가 생긴다(Kingdom Rush 의 영웅).
		//   WM 은 본편에 이미 조종하는 인형이 있으니 **한 명만 데려간다**가 세계관 정합이다.
		// ★ 왜 포탑과 같은 표를 쓰나: 전투 수치를 따로 두면 두 곳이 갈라진다. 다른 점은 단 하나 — 움직인다.

		/// <summary> 영웅이 판에 있는가. </summary>
		public bool HasHero => heroActive && heroTransform != null;

		/// <summary> 영웅 현재 위치(없으면 코어 자리). </summary>
		public Vector3 HeroPosition => heroTransform != null ? heroTransform.position.ToSim() : activeCorePosition;

		/// <summary> 영웅을 그 자리로 보낸다 — 걸어간다(순간이동 X, 늦는 것 자체가 판단의 대가다). </summary>
		public bool CommandHero(Vector3 worldPosition)
		{
			if (HasHero == false)
				return false;

			heroTargetPosition = new Vector3(worldPosition.x, heroTransform.position.y, worldPosition.z);
			return true;
		}

		private IEnumerator SpawnHeroRoutine()
		{
			if (stage.HeroUnit == null || stage.HeroUnit.Prefab == null)
				yield break; // 영웅 미설정 스테이지 — 기존 판과 완전히 동일하게 진행.

			Vector3 spawnPosition = stageRoot.TransformPoint(activeCorePosition.ToUnity()).ToSim() + new Vector3(stage.GroundCellSize * 1.5f, 0f, 0f);

			SpawnedUnit spawned = new();
			yield return SpawnUnitRoutine(stage.HeroUnit, spawnPosition,
				DEFENDER_TEAM, stage.HeroTint, stage.HeroScale, spawned);
			if (spawned.Ok == false)
				yield break;

			GameObject heroGameObject = spawned.GameObject;
			UnitObject heroUnitObject = spawned.UnitObject;
			heroCombatant = spawned.Combatant;

			// ★ 영웅의 자리는 *사람이 정한다*. 강체를 그대로 두면 내가 옮긴 좌표를 물리가 매 프레임 되돌린다
			//   (라이브 실측: 옮긴 다음 틱에 뒤로 밀려 제자리 — 명령해도 안 움직이는 것처럼 보였다).
			// ★ 켠 *다음 프레임*에 씌운다 — 스탯 배수와 같은 이유로, 켜기 전에 바꾼 값은 UnitObject.Start 의
			//   재-Init 규약에 조용히 덮인다(이 파일에서 이미 한 번 겪은 트랩).
			//   대여 계약(TowerDefenseUnitLease)이 반납 때 원래 값으로 되돌리므로 다음 대여(마수 등)에 안 샌다.
			yield return null;
			if (core == null || targeting == null || pool == null)
				yield break;

			Rigidbody heroBody = heroGameObject.GetComponent<Rigidbody>();
			if (heroBody != null)
			{
				heroBody.isKinematic = true;
				heroBody.useGravity = false;
			}

			// 길찾기 에이전트(NavMeshAgent)만 끈다 — 개척 지면은 런타임 생성이라 NavMesh 자체가 없고,
			// 켜두면 에이전트가 좌표를 도로 잡아당긴다(실측).
			UnityEngine.AI.NavMeshAgent heroAgent = heroGameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
			if (heroAgent != null)
				heroAgent.enabled = false;

			// ★ 이동 부품(UnitMovement)은 *켜 둔다*. 예전엔 이것까지 끄고 영웅 좌표를 매 틱 직접 옮겼는데,
			//   그 하나가 사용자 실측 결함 셋을 한꺼번에 만들었다:
			//   ① 뚝뚝 끊김 — 틱은 초당 20번이라 그 사이 프레임엔 영웅이 아예 안 움직인다(순간이동).
			//   ② 벽 통과 — 좌표를 직접 쓰면 충돌을 아무도 안 본다. 이동 부품은 쓸어보고 미끄러진다.
			//   ③ 마수가 밀려남 — 몸을 겹친 채 좌표만 옮기니 물리가 마수를 밀어내 해결한다.
			//   마수는 원래부터 이 부품으로 걷는다 — 영웅만 체계 밖에 있었다.
			heroMovement = heroGameObject.GetComponent<UnitMovement>();
			if (heroMovement != null)
				heroMovement.enabled = true;

			// 「초당 몇 칸」으로 적어둔 영웅 속도를 이동 부품이 읽는 스탯으로 옮긴다(환산 상수는 그쪽 정본).
			if (heroUnitObject != null)
			{
				heroUnitObject.UnitStat[UnitStatType.MOVEMENT_SPEED] =
					Mathf.Max(1, Mathf.RoundToInt(stage.HeroMoveSpeed * InputContributor.STAT_PER_UNIT_PER_SECOND));
			}


			if (stage.HeroArchetype != null)
			{
				TowerDefenseWeapon heroWeapon = heroUnitObject.GetComponent<TowerDefenseWeapon>();
				if (heroWeapon == null)
					heroWeapon = heroUnitObject.gameObject.AddComponent<TowerDefenseWeapon>();
				// 영웅은 포탑 연구가 아니라 *영웅 갈래*를 탄다 — 한 갈래를 뚫었는데 엉뚱한 게 세지면
				// 성좌를 보고 고른 뜻이 사라진다.
				heroWeapon.Configure(stage.HeroArchetype, targeting, heroCombatant, waveEnemies,
					IsVisibleAt,
					target => DamageMultiplierFor(target) * (1f + ResearchBonus(TowerDefenseResearchEffect.HeroPower)),
					() => Adaptation, () => TowerRangeMultiplier);
				heroWeapon.ReportNoise = ReportShotNoise;
			}

			// 표적 등록은 세우는 문이 이미 했다 — 여기서 또 하면 같은 것이 목록에 두 번 들어간다.

			// ★ 마수는 영웅을 *통과한다* (사용자 실증: "영웅 유닛으로 길막이 됨").
			//   이동이 몸통을 쓸어 미끄러지는 방식이라, 영웅을 길목에 세워두면 그 자체가 벽이 된다 —
			//   지어야 막는 게임에서 공짜 벽이다. 영웅은 여전히 지형·건물에 막히되(그건 유지),
			//   마수와의 몸싸움만 서로 무시한다. 때리는 것은 사거리로 하지 몸으로 하지 않는다.
			IgnoreCollisionsWithEnemies(heroGameObject);

			heroTransform = heroGameObject.transform;
			heroTargetPosition = heroTransform.position.ToSim();
			heroActive = true;
			// 영웅 칸은 영웅이 실제로 서야 생긴다 — 없는데 칸만 있으면 또 「눌리지 않는 칸」이다.
			RefreshAvailableSlots();
			SlotsChanged();

			// 영웅에게도 이름이 있어야 「데려간 아이」가 된다 — 이름 없는 영웅은 커서다.
			RegisterDoll(heroTransform, stage.HeroTint);
			RefreshHeroVision();
		}

		/// <summary>
		/// 영웅 이동 + 움직이는 시야. 건물 시야는 지어질 때 한 번만 계산하면 되지만 영웅은 매 틱 자리가 바뀌므로
		/// **칸이 바뀐 순간에만** 다시 계산한다(매 틱 전면 재계산은 44칸 판에서 그냥 낭비다).
		/// </summary>
		private void TickHero()
		{
			// 쓰러진 뒤 시계 — 다 되면 코어 옆에서 일어난다.
			if (heroActive == false && heroTransform != null && stage != null && stage.HeroRespawnSeconds > 0f)
			{
				heroRespawnRemaining -= TimeManager.TICK;
				if (heroRespawnRemaining <= 0f)
					ReviveHero();
				return;
			}

			if (HasHero == false)
				return;

			if (heroCombatant != null && heroCombatant.IsAlive == false)
			{
				// ★ 쓰러져도 영영 끝은 아니다(개선 목록 8번). 「한 명만 데려간다」의 무게는 *되돌리는 데
				//   드는 값*으로 표현한다 — 돌아올 방법이 하나도 없는 건 무게가 아니라 그냥 벽이다.
				heroActive = false;
				// 걷던 명령을 지운다 — 안 지우면 쓰러진 몸이 반납될 때까지 계속 걷는다.
				heroMovement?.SetMoveDirection(Vector3.zero.ToUnity());
				heroRespawnRemaining = stage.HeroRespawnSeconds;
				Debug.Log($"{nameof(TowerDefenseMatch)}: 영웅 쓰러짐 — {stage.HeroRespawnSeconds:F0}초 뒤 코어에서 일어난다.");
				if (coreCombatant != null)
					PopWorldText("영웅 쓰러짐", heroTransform.position.ToSim(), TextType.Warning);
				// ★ 월드에 뜨는 글자는 그 자리를 보고 있어야만 보인다 — 영웅은 대개 화면 밖에서 죽는다
				//   (혼자 정찰 나가 있으니까). 가장자리 알림으로도 알린다.
				alerts.Raise("영웅이 쓰러졌다", heroTransform != null ? heroTransform.position.ToSim() : coreCombatant.Position,
					Time.time, stage.AlertSeconds);
				return;
			}

			if (heroMovement == null)
				return;

			Vector3 delta = heroTargetPosition - heroTransform.position.ToSim();
			delta.y = 0f;

			// 도착 판정은 *한 틱에 갈 거리*로 잡는다 — 더 좁게 잡으면 목표를 지나쳤다 되돌아오길 반복하며 떤다.
			float arriveDistance = stage.HeroMoveSpeed * TimeManager.TICK;
			if (delta.sqrMagnitude <= arriveDistance * arriveDistance)
			{
				heroMovement.SetMoveDirection(Vector3.zero.ToUnity());
				return;
			}

			// 방향만 준다 — 실제로 얼마나 가는지는 이동 부품이 매 프레임 정한다(그래서 부드럽고, 벽에 막힌다).
			heroMovement.SetMoveDirection(delta.normalized.ToUnity());
			RefreshHeroVision();
		}

		private float heroRespawnRemaining;

		/// <summary> 영웅이 다시 일어나기까지 남은 시간(0 = 살아있음) — 화면이 「곧 온다」를 말한다. </summary>
		public float HeroRespawnIn => heroActive ? 0f : Mathf.Max(0f, heroRespawnRemaining);

		/// <summary>
		/// 쓰러진 영웅을 코어 옆에서 되살린다 — 자리·체력을 처음처럼 돌리되 *경험은 남긴다*
		/// (그 아이가 다른 아이가 되면 데려간 의미가 없다).
		/// </summary>
		private void ReviveHero()
		{
			if (heroTransform == null || heroCombatant == null || coreCombatant == null)
				return;

			UnitObject heroUnit = heroCombatant.UnitObject;
			if (heroUnit == null)
				return;

			heroTransform.position = (coreCombatant.Position + new Vector3(stage.GroundCellSize * 1.5f, 0f, 0f)).ToUnity();
			heroUnit.UnitStat[UnitStatType.HP_CUR] = heroUnit.UnitStat[UnitStatType.HP_MAX];
			heroTargetPosition = heroTransform.position.ToSim();
			heroActive = true;
			heroRespawnRemaining = 0f;

			PopWorldText("영웅 복귀", heroTransform.position.ToSim(), TextType.Heal);
			Debug.Log($"{nameof(TowerDefenseMatch)}: 영웅이 코어에서 다시 일어났다.");
		}

		private Vector2Int heroVisionCell = new Vector2Int(int.MinValue, int.MinValue);
		private int heroVisionSourceIndex = -1;

		private void RefreshHeroVision()
		{
			if (vision == null || mapLayout == null || stageRoot == null || heroTransform == null || stage.HeroVisionRadius <= 0f)
				return;

			Vector2Int cell = mapLayout.WorldToCell(stageRoot.InverseTransformPoint(heroTransform.position).ToSim());
			if (cell == heroVisionCell)
				return;

			heroVisionCell = cell;
			TowerDefenseVision.Source source = new(cell, stage.HeroVisionRadius);

			// 영웅의 시야원은 *하나*다 — 지나간 자리마다 원을 남기면 판이 통째로 밝아진다(밝힌 자리는
			// Explored 로 남으므로 「가봤다」는 기록은 그대로 유지된다).
			if (heroVisionSourceIndex >= 0 && heroVisionSourceIndex < visionSources.Count)
				visionSources[heroVisionSourceIndex] = source;
			else
			{
				heroVisionSourceIndex = visionSources.Count;
				visionSources.Add(source);
			}

			RefreshVision();
		}
	}
}
