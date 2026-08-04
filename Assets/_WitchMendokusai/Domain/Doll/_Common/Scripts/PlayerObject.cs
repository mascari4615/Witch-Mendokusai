using System.Collections;
using FMODUnity;
using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[RequireComponent(typeof(PlayerKnockbackCameraGlue))]
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
