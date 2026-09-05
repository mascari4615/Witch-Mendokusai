using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(Dash), menuName = "WM/Skill/" + nameof(Dash))]
	public class Dash : SkillData
	{
		[Header("Dash Tuning")]
		[SerializeField] private float dashSpeed = 15f;
		[SerializeField] private float dashDuration = 0.1f;

		public override void ActualUse(SkillContext context)
		{
			UnitObject unitObject = context.User;
			Vector3 direction = unitObject.UnitMovement.MoveDirectionWorld;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.0001f)
			{
				// 입력 방향이 없으면 캐릭터가 바라보는 쪽 기준 — IsLookingRight으로 X축 부호 결정.
				direction = unitObject.UnitMovement.IsLookingRight ? Vector3.right : Vector3.left;
			}
			else
			{
				direction.Normalize();
			}

			unitObject.UnitMovement.ApplyImpulse(direction * dashSpeed, dashDuration);
		}
	}
}
