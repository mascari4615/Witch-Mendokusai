using UnityEngine;

namespace WitchMendokusai
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(PlayerObject))]
	[RequireComponent(typeof(UnitMovement))]
	public class PlayerLandingCameraGlue : MonoBehaviour
	{
		[SerializeField] private float landingImpulseScale = 0.42f;
		[SerializeField] private float landingImpulseMinThreshold = 0.32f;
		[SerializeField] private float landingImpulseFloor = 0f;
		[SerializeField] private float landingImpulseExponent = 1.8f;
		[SerializeField] private float landingImpulseMaxAmplitude = 0.45f;

		private UnitMovement unitMovement;

		private void Awake()
		{
			unitMovement = GetComponent<UnitMovement>();
		}

		private void OnEnable()
		{
			if (unitMovement != null)
				unitMovement.OnLanded += HandleLanded;
		}

		private void OnDisable()
		{
			if (unitMovement != null)
				unitMovement.OnLanded -= HandleLanded;
		}

		private void HandleLanded(float impactStrength)
		{
			if (CameraManager.Instance == null)
				return;

			float threshold = Mathf.Max(landingImpulseMinThreshold, 0.32f);
			float floor = Mathf.Min(landingImpulseFloor, 0.02f);
			float scale = Mathf.Min(landingImpulseScale, 0.42f);
			float maxAmplitude = Mathf.Clamp(landingImpulseMaxAmplitude, 0f, 0.45f);

			float clamped = Mathf.Clamp01(impactStrength);
			if (clamped < threshold)
				return;

			// Remap after threshold so tiny landings stay silent while larger impacts scale smoothly.
			float normalized = Mathf.InverseLerp(threshold, 1f, clamped);
			float curved = Mathf.Pow(normalized, landingImpulseExponent);
			float impulse = floor + (curved * scale);
			impulse = Mathf.Min(impulse, maxAmplitude);
			CameraManager.Instance.GenerateCameraImpulse(impulse);
		}
	}
}
