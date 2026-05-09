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
			if (Player.Instance.AimPos == Vector3.zero)
			{
				animator.SetBool(MarkerEnabled, false);
				return;
			}

			if (lastNearestTarget != Player.Instance.NearestTarget)
			{
				lastNearestTarget = Player.Instance.NearestTarget;
				animator.SetTrigger(MarkerResetTrigger);
			}

			animator.SetBool(MarkerEnabled, true);
			transform.position = Player.Instance.AimPos;
		}
	}
}