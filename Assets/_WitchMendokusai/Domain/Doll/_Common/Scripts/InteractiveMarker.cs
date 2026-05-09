using UnityEngine;

namespace WitchMendokusai

{
	public class InteractiveMarker : MonoBehaviour
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private Animator animator;
		private InteractiveObject lastNearest;

		private void Awake()
		{
			animator = GetComponent<Animator>();
		}

		private void Update()
		{
			InteractiveObject nearest = InteractiveObject.GetNearest(Player.Instance.transform.position, PlayerInteraction.InteractionDistance);

			if (nearest == null)
			{
				animator.SetBool(MarkerEnabled, false);
				return;
			}

			if (lastNearest != nearest)
			{
				lastNearest = nearest;
				animator.SetTrigger(MarkerResetTrigger);
			}

			animator.SetBool(MarkerEnabled, true);
			transform.position = nearest.transform.position;
		}
	}
}