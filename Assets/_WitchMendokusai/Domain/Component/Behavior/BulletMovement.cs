using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class BulletMovement : SkillComponent
	{
		private Vector3 moveDirection;
		[SerializeField] private float moveSpeed;
		[SerializeField] private bool useAutoAim;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		public void SetMoveDirection(Vector3 newDirection)
		{
			moveDirection = newDirection;
		}

		private void Update()
		{
			transform.position += moveSpeed * Time.deltaTime * moveDirection;
		}

		public override void InitContext(SkillObject skillObject)
		{
			if (skillObject.UsedByPlayer)
			{
				if (useAutoAim == false)
				{
					moveDirection = playerProvider.Current.AimDirection;
					moveDirection.y = 0;
				}
				else
				{
					moveDirection = playerProvider.Current.NearestTarget != null
						? (playerProvider.Current.NearestTarget.position - transform.position).normalized
						: playerProvider.Current.AimDirection;
					moveDirection.y = 0;
				}
			}
			else
			{
				SetMoveDirection((playerProvider.Current.transform.position - skillObject.Context.User.transform.position).normalized);
			}
		}
	}
}
