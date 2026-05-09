using UnityEngine;

namespace WitchMendokusai
{
	public class AutoAimMarker : MonoBehaviour
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private Animator animator;
		private Transform lastNearestTarget;

		private void Awake()
		{
			animator = GetComponent<Animator>();
		}

		private void Update()
		{
			if (PlayerProvider.Instance.Current.AimPos == Vector3.zero)
			{
				animator.SetBool(MarkerEnabled, false);
				return;
			}

			if (lastNearestTarget != PlayerProvider.Instance.Current.NearestTarget)
			{
				lastNearestTarget = PlayerProvider.Instance.Current.NearestTarget;
				animator.SetTrigger(MarkerResetTrigger);
			}

			animator.SetBool(MarkerEnabled, true);
			transform.position = PlayerProvider.Instance.Current.AimPos;
		}
	}
}