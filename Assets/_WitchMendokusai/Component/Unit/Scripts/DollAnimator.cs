using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	public class DollAnimator : MonoBehaviour
	{
		[SerializeField] private Animator mainAnimator;
		[SerializeField] private Animator animatorOf3DModel;
		[SerializeField] private Transform pivot;

		[SerializeField] private new Rigidbody rigidbody;
		[SerializeField] private Animator handAnimator;

		private void Update()
		{
			bool isMoving = rigidbody.linearVelocity.magnitude > 0.1f;
			mainAnimator.SetBool("MOVE", isMoving);
			animatorOf3DModel.SetBool("MOVE", isMoving);

			handAnimator.SetBool("CHANNELING", Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed));

			Vector3 moveDirection = Player.Instance.Object.UnitMovement.MoveDirectionLocal;
			if (moveDirection.x == 0)
				return;

			float direction = Mathf.Sign(moveDirection.x);
			pivot.localScale = new Vector3(direction, 1, 1);
		}
	}
}