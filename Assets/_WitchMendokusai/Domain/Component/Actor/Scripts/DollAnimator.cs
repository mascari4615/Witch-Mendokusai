using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace WitchMendokusai
{
	public class DollAnimator : MonoBehaviour
	{
		[SerializeField] private Animator mainAnimator;
		[SerializeField] private Animator animatorOf3DModel;
		[SerializeField] private Transform pivot;
		[SerializeField] private Animator handAnimator;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void Update()
		{
			Vector3 velocity = playerProvider.CurrentObject.UnitMovement.Velocity;
			bool isMoving = new Vector2(velocity.x, velocity.z).sqrMagnitude > 0.01f;
			mainAnimator.SetBool("MOVE", isMoving);
			animatorOf3DModel.SetBool("MOVE", isMoving);

			handAnimator.SetBool("CHANNELING", Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed));

			Vector3 moveDirection = playerProvider.CurrentObject.UnitMovement.MoveDirectionLocal;
			if (moveDirection.x == 0)
				return;

			float direction = Mathf.Sign(moveDirection.x);
			pivot.localScale = new Vector3(direction, 1, 1);
		}
	}
}
