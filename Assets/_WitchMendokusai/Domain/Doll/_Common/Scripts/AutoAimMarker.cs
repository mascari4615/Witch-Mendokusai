using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class AutoAimMarker : MonoBehaviour
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private Animator animator;
		private Transform lastNearestTarget;
		private PlayerProvider playerProvider;

		private void Awake()
		{
			animator = GetComponent<Animator>();
		}

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void Update()
		{
			if (playerProvider.Current.AimPos == Vector3.zero)
			{
				animator.SetBool(MarkerEnabled, false);
				return;
			}

			if (lastNearestTarget != playerProvider.Current.NearestTarget)
			{
				lastNearestTarget = playerProvider.Current.NearestTarget;
				animator.SetTrigger(MarkerResetTrigger);
			}

			animator.SetBool(MarkerEnabled, true);
			transform.position = playerProvider.Current.AimPos;
		}
	}
}
