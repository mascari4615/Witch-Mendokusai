using UnityEngine;
// ★ 조준 셈(ProjectileAim)은 판정이다 (TASK-WM-214) — 엔진 값은 캐스트로 들여 넘긴다.
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
				// 조준 셈(ProjectileAim)은 판정 쪽이라 엔진 좌표를 캐스트로 들인다 (TASK-WM-214).
				Numerics.Vector3 origin = (Numerics.Vector3)skillObject.Context.User.transform.position;
				Numerics.Vector3? targetPosition = skillObject.Context.Target != null
					? (Numerics.Vector3)skillObject.Context.Target.transform.position
					: (Numerics.Vector3?)null;
				Numerics.Vector3? fallbackAim = playerProvider.Current != null
					? (Numerics.Vector3)playerProvider.Current.transform.position - origin
					: (Numerics.Vector3?)null;
				SetMoveDirection(ProjectileAim.Resolve(origin, targetPosition, fallbackAim, (Numerics.Vector3)transform.forward));
			}
		}
	}
}
