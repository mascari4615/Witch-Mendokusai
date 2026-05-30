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
			gameEventManager.Raise(GameEventType.OnPlayerDied);
			timeManager.DoSlowMotion();
			diedX.SetActive(true);
		}

		private IEnumerator InvincibleTime()
		{
			// TODO
			int invincibleTimeByDeciSec = (int)(soManager.InvincibleTime.RuntimeValue * 10);
			bool isWhite = false;

			while (invincibleTimeByDeciSec > 0)
			{
				invincibleTimeByDeciSec--;
				isWhite = !isWhite;

				SpriteRenderer.material.SetFloat("_Emission", isWhite ? 1 : 0);
				yield return new WaitForSeconds(.1f);
			}

			SpriteRenderer.material.SetFloat("_Emission", 0);
			invincibleRoutine = null;
		}
	}
}
