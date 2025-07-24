using UnityEngine;

namespace WitchMendokusai
{
	public class DollAnimator : MonoBehaviour
	{
		[SerializeField] private Animator mainAnimator;
		[SerializeField] private Transform pivot;

		[SerializeField] private new Rigidbody rigidbody;

		[SerializeField] private Animator handAnimator;

		private void Update()
		{
			if (rigidbody.linearVelocity.magnitude > 0.1f)
				mainAnimator.SetBool("MOVE", true);
			else
				mainAnimator.SetBool("MOVE", false);

			Vector3 localVelocity = transform.InverseTransformDirection(rigidbody.linearVelocity);
			if (localVelocity.x > 0)
				pivot.localScale = new Vector3(1, 1, 1);
			else if (localVelocity.x < 0)
				pivot.localScale = new Vector3(-1, 1, 1);

			handAnimator.SetBool("CHANNELING", Input.GetKey(KeyCode.Mouse0) || Input.GetKey(KeyCode.Mouse1));
		}
	}
}