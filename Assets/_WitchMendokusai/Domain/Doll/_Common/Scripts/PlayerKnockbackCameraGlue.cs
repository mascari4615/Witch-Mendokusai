using UnityEngine;
using VContainer;

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
		[SerializeField] private float forceScale = 0.04f;
		[SerializeField] private float maxAmplitude = 0.9f;
		[SerializeField] private float forceExponent = 0.85f;
		[SerializeField] private float forceThreshold = 0f;

		private UnitHealth unitHealth;
		private CameraManager cameraManager;

		private void Awake()
		{
			unitHealth = GetComponent<UnitHealth>();
		}

		[Inject]
		public void Construct(CameraManager cameraManager)
		{
			this.cameraManager = cameraManager;
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
			if (cameraManager == null)
				return;

			float force = Mathf.Max(0f, damageInfo.knockbackForce);
			float forceAmplitude = 0f;
			if (force > forceThreshold)
				forceAmplitude = Mathf.Pow(force - forceThreshold, forceExponent) * forceScale;

			float amplitude = Mathf.Clamp(Mathf.Max(minimumAmplitude, forceAmplitude), 0f, maxAmplitude);
			cameraManager.GenerateCameraImpulse(amplitude);
		}
	}
}
