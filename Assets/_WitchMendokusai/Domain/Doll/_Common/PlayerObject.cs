using System.Collections;
using FMODUnity;
using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// ★ 여기 있던 `[RequireComponent(typeof(PlayerKnockbackCameraGlue))]` 를 뗐다 (2026-08-06).
	//
	//   왜: `PlayerObject` 는 이제 **조종당하는 아바타 전용이 아니다** — 투기장 인형(WM-165)도
	//   이 껍데기를 쓴다(로스터 스폰이 요구). 그런데 카메라 임펄스는 *조종자 한 명*을 위한 연출이라,
	//   요구사항으로 박아두면 이 껍데기를 쓰는 **모든** 유닛이 전역 카메라를 흔든다.
	//
	//   실제로 그랬다: 투기장 인형마다 글루가 붙어 피격 때마다 관전 카메라가 흔들렸고
	//   (`minimumAmplitude = 0.18` 이라 넉백 0 인 타격도), 프리팹에서 부품을 지워도
	//   **Unity 가 이 attribute 때문에 임포트 때 도로 붙여서** 지울 수가 없었다.
	//   「파일엔 없는데 검사엔 있다」로 한참 헤맨 원인이 이것이다.
	//
	//   안전한 이유: 이 타입을 `GetComponent` 하는 코드가 **하나도 없고**(참조는 이 주석과 테스트뿐),
	//   진짜 플레이어(`Player.prefab`)엔 부품이 **직렬화되어 박혀 있다** — attribute 를 떼도 안 사라진다.
	//   「플레이어엔 있고 투기장 인형엔 없다」는 의도는 이제 `ArenaDollPrefabTests` 가 양쪽으로 잠근다
	//   (attribute 가 뭉뚱그려 표현하던 걸 기계 검사로 정확히 옮긴 것).
	public class PlayerObject : UnitObject
	{
		private Coroutine invincibleRoutine = null;
		[SerializeField] private GameObject diedX;

		[field: SerializeField] public Transform CameraPosition { get; private set; }
		[field: SerializeField] public Transform HeadAnchor { get; private set; }

		[SerializeField] private SpriteRenderer headRenderer;
		[SerializeField] private SpriteRenderer bodyRenderer;

		// 무적 시간 중 스프라이트 깜박임 주기(초). 깜박임 횟수도 이 값에서 파생 — 수치를 두 곳에 박지 않는다.
		// [Min] = 0 입력 시 0-나눗셈 + WaitForSeconds(0) 무한루프 차단. 방어 분기 대신 입력 자체를 막는다.
		[SerializeField, Min(0.001f)] private float invincibleBlinkInterval = 0.1f;

		private GameEventManager gameEventManager;
		private SOManager soManager;

		[Inject]
		public void Construct(GameEventManager gameEventManager, SOManager soManager, TimeManager timeManager, UnitStatCalculator unitStatCalculator,
			ObjectPoolManager objectPoolManager, PlayerProvider playerProvider)
		{
			this.gameEventManager = gameEventManager;
			this.soManager = soManager;
			SetBaseDeps(timeManager, unitStatCalculator, objectPoolManager, playerProvider);
		}

		public void SetDoll(int dollID)
		{
			Init(GetDoll(dollID));
		}

		// TASK-WM-163 — 1인칭 시 자기 스프라이트 숨김 (머리·몸통 빌보드).
		public void SetSelfVisible(bool visible)
		{
			if (headRenderer != null)
				headRenderer.enabled = visible;

			if (bodyRenderer != null)
				bodyRenderer.enabled = visible;
		}

		public override void Init(Unit unitData)
		{
			base.Init(unitData);

			headRenderer.sprite = UnitData.Sprites[0];
			bodyRenderer.sprite = UnitData.Sprites[1];

			diedX.SetActive(false);

			EventBusBridge.Publish(new PlayerObjectBoundEvent
			{
				UnitStat = UnitStat,
				UnitData = UnitData,
				Transform = transform,
				Object = this,
			});
		}

		public override void ReceiveDamage(DamageInfo damageInfo)
		{
			// 일반 피해는 피격 뒤 시작된 무적 시간 동안 막는다.
			// 낙사·검증용 강제 피해처럼 ignoreInvincible을 명시한 호출만 관통한다.
			if (damageInfo.ignoreInvincible == false && invincibleRoutine != null)
				return;

			base.ReceiveDamage(damageInfo);
		}

		private void OnEnable()
		{
			Health.OnTakeDamage += HandleDamageEffects;
			Health.OnDied += HandleDeathEffects;
		}

		private void OnDisable()
		{
			Health.OnTakeDamage -= HandleDamageEffects;
			Health.OnDied -= HandleDeathEffects;
		}

		private void HandleDamageEffects(DamageInfo damageInfo)
		{
			if (DungeonManagerBridge.IsDungeon == false)
				return;

			if (invincibleRoutine != null)
				return;

			RuntimeManager.PlayOneShot("event:/SFX/Monster/Hit", transform.position);
			gameEventManager.Raise(GameEventType.OnPlayerHit);
			// 카메라 셰이크는 PlayerKnockbackCameraGlue가 force 비례로 처리 — 여기서 호출 X.

			if (invincibleRoutine != null)
				StopCoroutine(invincibleRoutine);
			invincibleRoutine = StartCoroutine(InvincibleTime());

			switch (UnitStat[UnitStatType.HP_CUR])
			{
				case > 0:
					// Animator.SetTrigger("AHYA");
					break;
			}
		}

		protected virtual void HandleDeathEffects()
		{
			// 플레이어 사망 = 게임오버 이벤트 + 슬로모 = 전역 게임상태 → 던전에서만.
			// 아레나에 출전한 인형(Doll)이 죽어도 본진 게임이 끝나면 안 됨(MonsterObject 던전격리 선례 정합).
			// HandleDamageEffects 는 이미 IsDungeon 가드 — death 만 누락이었음(비대칭 해소).
			if (DungeonManagerBridge.IsDungeon)
			{
				gameEventManager.Raise(GameEventType.OnPlayerDied);
				timeManager.DoSlowMotion();
			}

			diedX.SetActive(true); // 사망 시각 표시(died X)는 컨텍스트 무관.
		}

		private IEnumerator InvincibleTime()
		{
			int blinkCount = (int)(soManager.InvincibleTime.RuntimeValue / invincibleBlinkInterval);
			bool isWhite = false;

			while (blinkCount > 0)
			{
				blinkCount--;
				isWhite = isWhite == false;

				SpriteRenderer.material.SetFloat("_Emission", isWhite ? 1 : 0);
				yield return new WaitForSeconds(invincibleBlinkInterval);
			}

			SpriteRenderer.material.SetFloat("_Emission", 0);
			invincibleRoutine = null;
		}
	}
}
