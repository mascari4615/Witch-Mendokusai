using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 직사각 띠 바닥 예고. Position (또는 Follow) 이 시작점, Direction 쪽으로 Length, 폭 Width
	/// 시작점에서 끝으로 차오름. 안쪽에 진행 방향 화살 무늬가 앞으로 흐름. 방향은 재생을 시작할 때 값으로 고정
	/// </summary>
	[CreateAssetMenu(fileName = "Telegraph_0_Line", menuName = "WM/CombatFx/" + nameof(GroundLineTelegraph))]
	public class GroundLineTelegraph : GroundTelegraph
	{
		private static readonly int ChevronSpacingId = Shader.PropertyToID("_ChevronSpacing");
		private static readonly int ChevronSlopeId = Shader.PropertyToID("_ChevronSlope");
		private static readonly int ChevronWidthId = Shader.PropertyToID("_ChevronWidth");
		private static readonly int ChevronSpeedId = Shader.PropertyToID("_ChevronSpeed");
		private static readonly int ChevronStrengthId = Shader.PropertyToID("_ChevronStrength");
		private static readonly int ChevronMarginId = Shader.PropertyToID("_ChevronMargin");
		private static readonly int LineEndEdgeId = Shader.PropertyToID("_LineEndEdge");

		[Header("띠")]
		[SerializeField, Tooltip("모서리 둥글림 (월드 단위). 폭의 절반보다 크면 폭의 절반까지만")] private float cornerRadius = 0.12f;
		[SerializeField, Range(0f, 1f), Tooltip("짧은 끝 변 가장자리 선 세기. 긴 변이 1")] private float lineEndEdge = 0.45f;

		[Header("화살 무늬")]
		[SerializeField, Tooltip("화살 사이 간격 (월드 단위)")] private float chevronSpacing = 0.7f;
		[SerializeField, Tooltip("화살 꺾임 기울기. 클수록 뾰족함")] private float chevronSlope = 0.6f;
		[SerializeField, Tooltip("화살 선 굵기 (월드 단위)")] private float chevronWidth = 0.05f;
		[SerializeField, Tooltip("화살이 앞으로 흐르는 빠르기 (초당 칸)")] private float chevronSpeed = 1.2f;
		[SerializeField, Range(0f, 1f), Tooltip("화살 진하기. 0 이면 없음")] private float chevronStrength = 0.6f;
		[SerializeField, Tooltip("화살이 가장자리에서 떨어지는 거리 (월드 단위)")] private float chevronMargin = 0.18f;

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

		protected override void ApplyShapeProperties(MaterialPropertyBlock block, CombatFxContext context)
		{
			block.SetFloat(LineEndEdgeId, lineEndEdge);
			block.SetFloat(ChevronSpacingId, chevronSpacing);
			block.SetFloat(ChevronSlopeId, chevronSlope);
			block.SetFloat(ChevronWidthId, chevronWidth);
			block.SetFloat(ChevronSpeedId, chevronSpeed);
			block.SetFloat(ChevronStrengthId, chevronStrength);
			block.SetFloat(ChevronMarginId, chevronMargin);
		}
	}
}
