using System.Collections;
using FMODUnity;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[RequireComponent(typeof(PlayerKnockbackCameraGlue))]
	public class PlayerObject : UnitObject
	{
		private Coroutine invincibleRoutine = null;
		[SerializeField] private GameObject diedX;

		[field: SerializeField] public Transform CameraPosition { get; private set; }
		[field: SerializeField] public Transform SpritePosition { get; private set; }

		[SerializeField] private SpriteRenderer headRenderer;
		[SerializeField] private SpriteRenderer bodyRenderer;

		public void SetDoll(int dollID)
		{
			Init(GetDoll(dollID));
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
			if (DungeonManager.Instance.IsDungeon == false)
				return;

			if (invincibleRoutine != null)
				return;

			RuntimeManager.PlayOneShot("event:/SFX/Monster/Hit", transform.position);
			GameEventManager.Instance.Raise(GameEventType.OnPlayerHit);
			// 카메라 셰이크는 PlayerKnockbackCameraGlue가 force 비례로 처리 — 여기서 호출 X.

			if (invincibleRoutine != null)
				StopCoroutine(invincibleRoutine);
			invincibleRoutine = StartCoroutine(InvincibleTime());

			/*
			ObjectManager.Instance.PopObject("Effect_Hit",
				transform.position + (Vector3.Normalize(Wakgood.Instance.transform.position - transform.position) * .5f));*/

			switch (UnitStat[UnitStatType.HP_CUR])
			{
				case > 0:
					// Animator.SetTrigger("AHYA");
					break;
			}
		}

		protected virtual void HandleDeathEffects()
		{
			GameEventManager.Instance.Raise(GameEventType.OnPlayerDied);
			TimeManager.Instance.DoSlowMotion();
			diedX.SetActive(true);
		}

		private IEnumerator InvincibleTime()
		{
			// TODO
			int invincibleTimeByDeciSec = (int)(SOManager.Instance.InvincibleTime.RuntimeValue * 10);
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