using DG.Tweening;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Hit 시 victim 만 잠깐 멈춰 손맛 추가. timescale은 건드리지 않음 — *per-victim* 정지.
	/// - UnitMovement.Pause(duration) — Motor tick skip
	/// - DG Tween (스케일 부풀림 등) 일시 정지/재개
	///
	/// Animator 정지는 캐릭터마다 Animator 위치 다르고 옵션이라 단계 follow-up.
	/// </summary>
	[DisallowMultipleComponent]
	public class HitstopFeedback : MonoBehaviour
	{
		private UnitObject unitObject;
		private UnitHealth unitHealth;
		private UnitMovement unitMovement;

		private void Awake()
		{
			unitObject = GetComponent<UnitObject>();
			unitHealth = GetComponent<UnitHealth>();
			unitMovement = GetComponent<UnitMovement>();
		}

		private void OnEnable()
		{
			if (unitHealth != null)
				unitHealth.OnTakeDamage += HandleHitstop;
		}

		private void OnDisable()
		{
			if (unitHealth != null)
				unitHealth.OnTakeDamage -= HandleHitstop;
		}

		private void HandleHitstop(DamageInfo damageInfo)
		{
			if (damageInfo.hitstopDuration <= 0f)
				return;
			if (unitObject != null && unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return;

			if (unitMovement != null)
				unitMovement.Pause(damageInfo.hitstopDuration);

			// MeshParent 의 진행 중인 DOTween (UnitHealth.ReceiveDamage 의 스케일 연출 등) 잠시 정지.
			if (unitObject != null && unitObject.MeshParent != null)
			{
				Transform meshParent = unitObject.MeshParent;
				DOTween.Pause(meshParent);
				DOVirtual.DelayedCall(damageInfo.hitstopDuration, () =>
				{
					if (meshParent != null)
						DOTween.Play(meshParent);
				}, ignoreTimeScale: false);
			}
		}
	}
}
