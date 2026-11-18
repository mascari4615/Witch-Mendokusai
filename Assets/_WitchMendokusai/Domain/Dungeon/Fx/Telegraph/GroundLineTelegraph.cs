using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 직사각 띠 바닥 예고. Position (또는 Follow) 이 시작점, Direction 쪽으로 Length, 폭 Width
	/// 시작점에서 끝으로 차오름. 방향은 재생을 시작할 때 값으로 고정
	/// </summary>
	[CreateAssetMenu(fileName = "Telegraph_0_Line", menuName = "WM/CombatFx/" + nameof(GroundLineTelegraph))]
	public class GroundLineTelegraph : GroundTelegraph
	{
		[Header("띠")]
		[SerializeField, Tooltip("모서리 둥글림 (월드 단위). 폭의 절반보다 크면 폭의 절반까지만")] private float cornerRadius = 0.12f;

		protected override float ShapeValue => 1f;

		protected override float CornerRadius => cornerRadius;

		protected override Vector2 GetShapeHalfSize(CombatFxContext context) => new Vector2(context.Width, context.Length) / 2f;

		protected override Vector3 GetCenterOffset(CombatFxContext context, Vector3 direction) => direction * (context.Length / 2f);

		protected override Vector3 ResolveDirection(CombatFxContext context)
		{
			Vector3 direction = context.Direction;
			direction.y = 0f;
			if (direction.sqrMagnitude <= 0f)
				throw new System.ArgumentException($"{name}: 띠 예고에 수평 방향이 없음 (Direction {context.Direction})");
			return direction.normalized;
		}
	}
}
