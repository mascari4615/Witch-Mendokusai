using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	public class DollAnimator : MonoBehaviour
	{
		[SerializeField] private Animator mainAnimator;
		[SerializeField] private Animator animatorOf3DModel;
		[SerializeField] private Transform pivot;
		[SerializeField] private Animator handAnimator;

		private void Update()
		{
			// Kinematic Rigidbody는 linearVelocity가 항상 0 → Motor가 결정한 Velocity를 직접 읽는다.
			Vector3 velocity = PlayerProvider.Instance.CurrentObject.UnitMovement.Velocity;
			bool isMoving = new Vector2(velocity.x, velocity.z).sqrMagnitude > 0.01f;
			mainAnimator.SetBool("MOVE", isMoving);
			animatorOf3DModel.SetBool("MOVE", isMoving);

			handAnimator.SetBool("CHANNELING", Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed));

			Vector3 moveDirection = PlayerProvider.Instance.CurrentObject.UnitMovement.MoveDirectionLocal;
			if (moveDirection.x == 0)
				return;

			float direction = Mathf.Sign(moveDirection.x);
			pivot.localScale = new Vector3(direction, 1, 1);
		}
	}
}