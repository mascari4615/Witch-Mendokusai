using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 원형 바닥 예고. Position (또는 Follow) 이 중심, Radius 가 반지름. 중심에서 바깥으로 차오름
	/// 안쪽에 천천히 도는 마법진 무늬 (안쪽 고리, 룬 획, 별 다각형)
	/// 안쪽 화살을 켜면 마법진 대신 중심으로 흐르는 V 화살 (끌어당김 범위)
	/// </summary>
	[CreateAssetMenu(fileName = "Telegraph_0_Circle", menuName = "WM/CombatFx/" + nameof(GroundCircleTelegraph))]
	public class GroundCircleTelegraph : GroundTelegraph
	{
		private static readonly int InnerRingInsetId = Shader.PropertyToID("_InnerRingInset");
		private static readonly int InnerRingWidthId = Shader.PropertyToID("_InnerRingWidth");
		private static readonly int PatternLineWidthId = Shader.PropertyToID("_PatternLineWidth");
		private static readonly int RuneCountId = Shader.PropertyToID("_RuneCount");
		private static readonly int RuneLengthId = Shader.PropertyToID("_RuneLength");
		private static readonly int RuneLengthMinId = Shader.PropertyToID("_RuneLengthMin");
		private static readonly int RuneCrossChanceId = Shader.PropertyToID("_RuneCrossChance");
		private static readonly int RuneCrossWidthId = Shader.PropertyToID("_RuneCrossWidth");
		private static readonly int StarPointsId = Shader.PropertyToID("_StarPoints");
		private static readonly int StarStepId = Shader.PropertyToID("_StarStep");
		private static readonly int StarRadiusId = Shader.PropertyToID("_StarRadius");
		private static readonly int PatternSpeedId = Shader.PropertyToID("_PatternSpeed");
		private static readonly int InwardArrowsId = Shader.PropertyToID("_InwardArrows");
		private static readonly int ArrowSectorsId = Shader.PropertyToID("_ArrowSectors");
		private static readonly int ArrowArmWidthId = Shader.PropertyToID("_ArrowArmWidth");
		private static readonly int ArrowInnerClearId = Shader.PropertyToID("_ArrowInnerClear");
		private static readonly int ArrowArmSoftnessId = Shader.PropertyToID("_ArrowArmSoftness");
		private static readonly int ChevronSpacingId = Shader.PropertyToID("_ChevronSpacing");
		private static readonly int ChevronSlopeId = Shader.PropertyToID("_ChevronSlope");
		private static readonly int ChevronWidthId = Shader.PropertyToID("_ChevronWidth");
		private static readonly int ChevronSpeedId = Shader.PropertyToID("_ChevronSpeed");
		private static readonly int ChevronStrengthId = Shader.PropertyToID("_ChevronStrength");
		private static readonly int ChevronMarginId = Shader.PropertyToID("_ChevronMargin");

		[Header("마법진")]
		[SerializeField, Tooltip("가장자리에서 안쪽 고리까지의 거리 (월드 단위)")] private float innerRingInset = 0.32f;
		[SerializeField, Tooltip("안쪽 고리 굵기 (월드 단위)")] private float innerRingWidth = 0.016f;
		[SerializeField, Tooltip("룬 획과 별 선 굵기 (월드 단위)")] private float patternLineWidth = 0.016f;
		[SerializeField, Min(1f), Tooltip("둘레 한 바퀴의 룬 획 수 (정수로 내림)")] private float runeCount = 32f;
		[SerializeField, Tooltip("룬 획 최대 길이 (월드 단위). 안쪽 고리에서 가장자리 쪽으로 뻗음")] private float runeLength = 0.16f;
		[SerializeField, Range(0f, 1f), Tooltip("가장 짧은 룬 획의 길이 비율")] private float runeLengthMin = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip("룬 획에 가로 획이 붙을 확률")] private float runeCrossChance = 0.35f;
		[SerializeField, Tooltip("룬 가로 획 길이 (월드 단위)")] private float runeCrossWidth = 0.07f;
		[SerializeField, Range(3f, 8f), Tooltip("별 꼭짓점 수 (5 오각별, 6 육각별)")] private float starPoints = 6f;
		[SerializeField, Min(1f), Tooltip("별 꼭짓점을 몇 칸씩 건너 잇는지. 2 면 별 모양, 1 이면 다각형")] private float starStep = 2f;
		[SerializeField, Range(0f, 1f), Tooltip("별 크기. 안쪽 고리 반지름에 대한 비율")] private float starRadius = 0.72f;
		[SerializeField, Tooltip("마법진 회전 빠르기 (초당 라디안). 별은 반대로 돎")] private float patternSpeed = 0.12f;

		[Header("안쪽 화살 (끌어당김)")]
		[SerializeField, Tooltip("켜면 마법진 대신 중심으로 흐르는 화살. 끌어당김 범위 표시")] private bool inwardArrows = false;
		[SerializeField, Min(1f), Tooltip("화살 줄 수 (둘레를 나눈 칸)")] private float arrowSectors = 8f;
		[SerializeField, Range(0f, 0.5f), Tooltip("화살 팔 폭. 칸 폭에 대한 비율 (0.5 면 칸 끝까지)")] private float arrowArmWidth = 0.36f;
		[SerializeField, Range(0f, 0.2f), Tooltip("화살 팔 끝 흐림. 칸 폭에 대한 비율")] private float arrowArmSoftness = 0.05f;
		[SerializeField, Tooltip("중심에서 화살을 비우는 반지름 (월드 단위)")] private float arrowInnerClear = 1f;
		[SerializeField, Tooltip("화살 사이 간격 (월드 단위)")] private float arrowSpacing = 1.2f;
		[SerializeField, Tooltip("화살 꺾임 기울기")] private float arrowSlope = 0.5f;
		[SerializeField, Tooltip("화살 선 굵기 (월드 단위)")] private float arrowWidth = 0.14f;
		[SerializeField, Tooltip("화살이 안쪽으로 흐르는 빠르기 (초당 칸)")] private float arrowSpeed = 1.1f;
		[SerializeField, Range(0f, 1f), Tooltip("화살 진하기")] private float arrowStrength = 0.85f;
		[SerializeField, Tooltip("화살이 가장자리에서 떨어지는 거리 (월드 단위)")] private float arrowMargin = 0.3f;

		protected override float ShapeValue => 0f;

		protected override Vector2 GetShapeHalfSize(CombatFxContext context) => new(context.Radius, context.Radius);

		protected override Vector3 GetCenterOffset(CombatFxContext context, Vector3 direction) => Vector3.zero;

		// 원은 방향이 없음
		protected override Vector3 ResolveDirection(CombatFxContext context) => Vector3.forward;

		protected override void ApplyShapeProperties(MaterialPropertyBlock block)
		{
			block.SetFloat(InnerRingInsetId, innerRingInset);
			block.SetFloat(InnerRingWidthId, innerRingWidth);
			block.SetFloat(PatternLineWidthId, patternLineWidth);
			block.SetFloat(RuneCountId, runeCount);
			block.SetFloat(RuneLengthId, runeLength);
			block.SetFloat(RuneLengthMinId, runeLengthMin);
			block.SetFloat(RuneCrossChanceId, runeCrossChance);
			block.SetFloat(RuneCrossWidthId, runeCrossWidth);
			block.SetFloat(StarPointsId, starPoints);
			block.SetFloat(StarStepId, starStep);
			block.SetFloat(StarRadiusId, starRadius);
			block.SetFloat(PatternSpeedId, patternSpeed);
			block.SetFloat(InwardArrowsId, inwardArrows ? 1f : 0f);
			if (inwardArrows == false)
				return;
			block.SetFloat(ArrowSectorsId, arrowSectors);
			block.SetFloat(ArrowArmWidthId, arrowArmWidth);
			block.SetFloat(ArrowInnerClearId, arrowInnerClear);
			block.SetFloat(ArrowArmSoftnessId, arrowArmSoftness);
			block.SetFloat(ChevronSpacingId, arrowSpacing);
			block.SetFloat(ChevronSlopeId, arrowSlope);
			block.SetFloat(ChevronWidthId, arrowWidth);
			block.SetFloat(ChevronSpeedId, arrowSpeed);
			block.SetFloat(ChevronStrengthId, arrowStrength);
			block.SetFloat(ChevronMarginId, arrowMargin);
		}
	}
}
