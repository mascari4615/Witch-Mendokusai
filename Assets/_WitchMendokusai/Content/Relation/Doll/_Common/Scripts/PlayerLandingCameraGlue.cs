using UnityEngine;

namespace WitchMendokusai
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(PlayerObject))]
	[RequireComponent(typeof(UnitMovement))]
	public class PlayerLandingCameraGlue : MonoBehaviour
	{
		[SerializeField] private float landingImpulseScale = 0.95f;
		[SerializeField] private float landingImpulseMinThreshold = 0.12f;
		[SerializeField] private float landingImpulseFloor = 0.08f;
		[SerializeField] private float landingImpulseExponent = 1.35f;

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

			float clamped = Mathf.Clamp01(impactStrength);
			if (clamped < landingImpulseMinThreshold)
				return;

			float curved = Mathf.Pow(clamped, landingImpulseExponent);
			float impulse = landingImpulseFloor + (curved * landingImpulseScale);
			CameraManager.Instance.GenerateCameraImpulse(impulse);
		}
	}
}
