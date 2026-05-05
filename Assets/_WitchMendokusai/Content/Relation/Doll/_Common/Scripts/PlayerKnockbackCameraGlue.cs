using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Player 가 hit 받을 때 knockback force 비례로 카메라 cinemachine impulse 발생.
	/// PlayerLandingCameraGlue 패턴 정확히 따름 — SerializeField 로 scale / threshold / exponent 튜닝 가능.
	///
	/// force == 0 (예: 환경 데미지 / 약 hit) 인 hit 도 minimumAmplitude 만큼 minimum 셰이크 보장 —
	/// "맞은 느낌" 의 기본선.
	/// </summary>
	[DisallowMultipleComponent]
	public class PlayerKnockbackCameraGlue : MonoBehaviour
	{
		[SerializeField] private float minimumAmplitude = 0.18f;
		[SerializeField] private float forceScale = 0.04f;       // force 12 → +0.48, force 18 → +0.72
		[SerializeField] private float maxAmplitude = 0.9f;
		[SerializeField] private float forceExponent = 0.85f;    // < 1: 작은 force도 어느정도 시원함, 큰 force는 cap
		[SerializeField] private float forceThreshold = 0f;       // 이 값 이하의 force 는 minimum 만 적용

		private UnitHealth unitHealth;

		private void Awake()
		{
			unitHealth = GetComponent<UnitHealth>();
		}

		private void OnEnable()
		{
			if (unitHealth != null)
				unitHealth.OnTakeDamage += HandleHitShake;
		}

		private void OnDisable()
		{
			if (unitHealth != null)
				unitHealth.OnTakeDamage -= HandleHitShake;
		}

		private void HandleHitShake(DamageInfo damageInfo)
		{
			if (CameraManager.Instance == null)
				return;

			float force = Mathf.Max(0f, damageInfo.knockbackForce);
			float forceAmplitude = 0f;
			if (force > forceThreshold)
				forceAmplitude = Mathf.Pow(force - forceThreshold, forceExponent) * forceScale;

			float amplitude = Mathf.Clamp(Mathf.Max(minimumAmplitude, forceAmplitude), 0f, maxAmplitude);
			CameraManager.Instance.GenerateCameraImpulse(amplitude);
		}
	}
}
