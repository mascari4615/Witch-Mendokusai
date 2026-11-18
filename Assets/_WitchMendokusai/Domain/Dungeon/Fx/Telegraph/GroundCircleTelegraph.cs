using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 원형 바닥 예고. Position (또는 Follow) 이 중심, Radius 가 반지름. 중심에서 바깥으로 차오름
	/// </summary>
	[CreateAssetMenu(fileName = "Telegraph_0_Circle", menuName = "WM/CombatFx/" + nameof(GroundCircleTelegraph))]
	public class GroundCircleTelegraph : GroundTelegraph
	{
		protected override float ShapeValue => 0f;

		protected override Vector2 GetShapeHalfSize(CombatFxContext context) => new(context.Radius, context.Radius);

		protected override Vector3 GetCenterOffset(CombatFxContext context, Vector3 direction) => Vector3.zero;

		// 원은 방향이 없음
		protected override Vector3 ResolveDirection(CombatFxContext context) => Vector3.forward;
	}
}
