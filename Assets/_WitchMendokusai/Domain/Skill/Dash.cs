using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(Dash), menuName = "WM/Skill/" + nameof(Dash))]
	public class Dash : SkillData
	{
		[Header("Dash Tuning")]
		[SerializeField] private float dashSpeed = 15f;
		[SerializeField] private float dashDuration = 0.1f;

		[Header("잔상")]
		[SerializeField, Tooltip("잔상 겉모습 (사망 잔상 에셋 형식). 비우면 잔상 없음")] private MonsterDeathStyle afterimageStyle;
		[SerializeField, Min(1), Tooltip("남기는 잔상 수")] private int afterimageCount = 3;
		[SerializeField, Min(.01f), Tooltip("잔상 사이 간격 초")] private float afterimageInterval = .035f;
		[SerializeField, Tooltip("잔상 색 곱. 알파가 진하기")] private Color afterimageTint = new(.75f, .85f, 1f, .55f);

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
			if (afterimageStyle != null && unitObject.SpriteRenderer != null)
				DashAfterimage.Play(unitObject.SpriteRenderer, afterimageStyle, afterimageCount, afterimageInterval, afterimageTint);
		}
	}
}
