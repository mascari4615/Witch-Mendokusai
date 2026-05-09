using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public class BulletMovement : SkillComponent
	{
		private Vector3 moveDirection;
		[SerializeField] private float moveSpeed;
		[SerializeField] private bool useAutoAim;

		public void SetMoveDirection(Vector3 newDirection)
		{
			moveDirection = newDirection;
		}

		// Update is called once per frame
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
					moveDirection = PlayerProvider.Instance.Current.AimDirection;
					moveDirection.y = 0;
				}
				else
				{
					moveDirection = PlayerProvider.Instance.Current.NearestTarget != null
						? (PlayerProvider.Instance.Current.NearestTarget.position - transform.position).normalized
						: PlayerProvider.Instance.Current.AimDirection;
					moveDirection.y = 0;
				}
			}
			else
			{
				SetMoveDirection((PlayerProvider.Instance.Current.transform.position - skillObject.Context.User.transform.position).normalized);
			}
		}
	}
}