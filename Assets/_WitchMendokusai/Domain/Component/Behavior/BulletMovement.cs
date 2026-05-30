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
				// WM-165: 비-플레이어(아레나 AI 등) 발사 — 전술 타겟 우선 호밍, 없으면 레거시(플레이어 위치). Current null 가드.
				Vector3 origin = skillObject.Context.User.transform.position;
				Vector3? targetPosition = skillObject.Context.Target != null
					? skillObject.Context.Target.transform.position
					: (Vector3?)null;
				Vector3? fallbackAim = playerProvider.Current != null
					? playerProvider.Current.transform.position - origin
					: (Vector3?)null;
				SetMoveDirection(ProjectileAim.Resolve(origin, targetPosition, fallbackAim, transform.forward));
			}
		}
	}
}
