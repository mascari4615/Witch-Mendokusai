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
			mainAnimator.SetBool("MOVE", rigidbody.linearVelocity.magnitude > 0.1f);
			handAnimator.SetBool("CHANNELING", Input.GetKey(KeyCode.Mouse0) || Input.GetKey(KeyCode.Mouse1));

			Vector3 moveDirection = Player.Instance.Movement.MoveDirectionLocal;
			if (moveDirection.x == 0)
				return;

			float direction = Mathf.Sign(moveDirection.x);
			pivot.localScale = new Vector3(direction, 1, 1);
		}
	}
}