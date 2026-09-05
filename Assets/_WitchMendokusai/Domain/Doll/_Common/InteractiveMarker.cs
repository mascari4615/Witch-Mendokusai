using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class InteractiveMarker : MonoBehaviour
	{
		private const string MarkerEnabled = "ENABLED";
		private const string MarkerResetTrigger = "RESET";

		private Animator animator;
		private InteractiveObject lastNearest;
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
			InteractiveObject nearest = InteractiveObject.GetNearest(playerProvider.Current.transform.position, PlayerInteraction.InteractionDistance);

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
