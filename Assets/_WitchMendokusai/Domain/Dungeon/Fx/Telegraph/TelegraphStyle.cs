using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 바닥 예고의 겉모습 한 벌 (색, 굵기, 채움 방식, 등장과 끝). 모양 (원, 띠) 과 따로 둔 에셋
	/// - 모양 에셋 (GroundCircleTelegraph, GroundLineTelegraph) 이 이 에셋 하나를 고름. 같은 겉모습을 원과 띠가 같이 씀
	/// - 패턴 수치 에셋은 모양 에셋 하나를 고름. 겉모습만 바꾸려면 이 에셋을 바꾸거나 모양 에셋의 칸만 바꿈
	/// - 색 규칙: 피해는 붉은 주황, 끌어당김은 보라, 반격 창은 하늘색
	/// </summary>
	[CreateAssetMenu(fileName = "TelegraphStyle_0", menuName = "WM/CombatFx/" + nameof(TelegraphStyle))]
	public sealed class TelegraphStyle : ScriptableObject
	{
		[Header("그리기")]
		[SerializeField, Tooltip("GroundTelegraph 셰이더를 쓰는 재질. 색과 굵기는 이 에셋의 값이 덮어씀")] private Material material;

		[Header("색")]
		[SerializeField, Tooltip("안쪽 얇은 막 색. 알파는 0.08~0.15 정도로 거의 투명하게")] private Color baseColor = new(1f, 0.35f, 0.22f, 0.1f);
		[SerializeField, Tooltip("차오른 막 색. 얇은 막보다 조금 짙게")] private Color fillColor = new(1f, 0.32f, 0.18f, 0.22f);
		[SerializeField, Tooltip("차오르는 맨 앞 선 색")] private Color frontColor = new(1f, 0.85f, 0.62f, 1f);
		[SerializeField, Tooltip("가장자리 색 띠와 번짐 색. 알파는 색 띠의 진하기")] private Color edgeColor = new(1f, 0.38f, 0.2f, 0.85f);
		[SerializeField, Tooltip("가장자리 가운데 심 색. 거의 흰색에 가깝게")] private Color coreColor = new(1f, 0.93f, 0.82f, 1f);
		[SerializeField, Tooltip("안쪽 무늬 색 (원은 마법진이나 안쪽 화살, 띠는 화살 무늬). 알파를 테두리보다 확실히 낮게")] private Color patternColor = new(1f, 0.55f, 0.35f, 0.22f);
		[SerializeField, Tooltip("가장자리 위 반짝이는 점 색")] private Color sparkColor = new(1f, 0.85f, 0.6f, 1f);
		[SerializeField, Tooltip("발동 순간 번쩍임과 충격파 색")] private Color flashColor = new(1f, 0.9f, 0.75f, 1f);

		[Header("막")]
		[SerializeField, Range(0f, 1f), Tooltip("얇은 막의 가운데 진하기 비율. 가장자리 쪽이 1")] private float membraneCenter = 0.55f;
		[SerializeField, Range(0f, 1f), Tooltip("차오른 막의 안쪽 진하기 비율. 앞선 쪽이 1")] private float fillDepthShade = 0.65f;

		[Header("가장자리")]
		[SerializeField, Tooltip("가장자리 색 띠 굵기 (월드 단위)")] private float edgeWidth = 0.06f;
		[SerializeField, Tooltip("가장자리 가운데 밝은 심 굵기 (월드 단위)")] private float coreWidth = 0.022f;
		[SerializeField, Tooltip("심과 앞선의 빛 세기. 1 넘으면 HDR 카메라에서 번짐")] private float coreIntensity = 1.6f;
		[SerializeField, Tooltip("가장자리 번짐 폭 (월드 단위)")] private float glowWidth = 0.3f;
		[SerializeField, Tooltip("번짐 세기 (더하기)")] private float glowStrength = 0.8f;
		[SerializeField, Range(0f, 1f), Tooltip("안쪽으로 번지는 비율. 바깥이 1")] private float glowInner = 0.35f;
		[SerializeField, Range(0f, 1f), Tooltip("번짐이 밝은 바닥 위에서도 보이게 깔리는 덮기 진하기")] private float glowOpacity = 0.2f;
		[SerializeField, Range(0f, 1f), Tooltip("가장자리 맥동 깊이. 0 이면 안 뜀")] private float pulseAmount = 0.2f;
		[SerializeField, Tooltip("가장자리 맥동 빠르기 (초당 라디안)")] private float pulseSpeed = 7f;

		[Header("차오르는 앞선")]
		[SerializeField, Tooltip("앞선 굵기의 절반 (월드 단위)")] private float frontWidth = 0.018f;
		[SerializeField, Tooltip("앞선 뒤로 번지는 빛의 폭 (월드 단위)")] private float frontGlowWidth = 0.5f;
		[SerializeField, Tooltip("앞선 뒤 빛의 더하기 세기")] private float frontGlowAdd = 0.35f;

		[Header("무늬 공통")]
		[SerializeField, Tooltip("차오른 곳에서 무늬가 짙어지는 배율")] private float patternFilledBoost = 1.6f;
		[SerializeField, Tooltip("무늬의 더하기 빛 세기")] private float patternGlow = 0.25f;

		[Header("반짝이는 점")]
		[SerializeField, Tooltip("점 사이 간격 (월드 단위). 원은 둘레에 맞게 조금 조정됨")] private float sparkSpacing = 0.55f;
		[SerializeField, Tooltip("점 크기 (월드 단위)")] private float sparkSize = 0.035f;
		[SerializeField, Tooltip("점이 가장자리에서 벗어나는 폭 (월드 단위)")] private float sparkJitter = 0.04f;
		[SerializeField, Tooltip("점이 가장자리를 따라 흐르는 빠르기 (초당 칸)")] private float sparkSpeed = 0.6f;
		[SerializeField, Tooltip("깜빡임 빠르기 (초당 라디안)")] private float sparkTwinkleSpeed = 5f;
		[SerializeField, Tooltip("점 밝기. 0 이면 없음")] private float sparkStrength = 1.4f;

		[Header("빛")]
		[SerializeField, Tooltip("빛 성분 전체 배율 (더하기)")] private float addStrength = 1f;

		[Header("채움 방식")]
		[SerializeField, Tooltip("차오름 곡선. 가로 0~1 시간 비율, 세로 0~1 진행도")] private AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
		[SerializeField, Tooltip("나타날 때 가장자리가 한 바퀴 그려지는 시간 (초)")] private float appearSeconds = 0.16f;
		[SerializeField, Tooltip("나타날 때 시작 크기 배율. 1 이면 크기 변화 없음")] private float appearStartScale = 0.82f;

		[Header("끝 번쩍임")]
		[SerializeField, Tooltip("끝 번쩍임과 충격파가 사라지는 데 걸리는 시간 (초)")] private float flashSeconds = 0.28f;
		[SerializeField, Range(0f, 1f), Tooltip("발동 순간 안쪽 번쩍임 세기. 1 이면 안쪽이 번쩍임 색으로 완전히 덮임. 넓은 예고는 낮게")] private float flashStrength = 0.3f;
		[SerializeField, Tooltip("발동 순간 가장자리가 밝아지는 배율")] private float flashEdgeBoost = 2.5f;
		[SerializeField, Tooltip("발동 충격파가 가장자리에서 퍼지는 거리 (월드 단위)")] private float shockDistance = 0.9f;
		[SerializeField, Tooltip("발동 충격파 굵기의 절반 (월드 단위)")] private float shockWidth = 0.06f;

		[Header("소리")]
		[SerializeField, Tooltip("끄면 이 겉모습을 쓰는 예고는 시작음과 끝음이 없음")] private bool playSfx = true;

		public Material Material => material;
		public Color BaseColor => baseColor;
		public Color FillColor => fillColor;
		public Color FrontColor => frontColor;
		public Color EdgeColor => edgeColor;
		public Color CoreColor => coreColor;
		public Color PatternColor => patternColor;
		public Color SparkColor => sparkColor;
		public Color FlashColor => flashColor;
		public float MembraneCenter => membraneCenter;
		public float FillDepthShade => fillDepthShade;
		public float EdgeWidth => edgeWidth;
		public float CoreWidth => coreWidth;
		public float CoreIntensity => coreIntensity;
		public float GlowWidth => glowWidth;
		public float GlowStrength => glowStrength;
		public float GlowInner => glowInner;
		public float GlowOpacity => glowOpacity;
		public float PulseAmount => pulseAmount;
		public float PulseSpeed => pulseSpeed;
		public float FrontWidth => frontWidth;
		public float FrontGlowWidth => frontGlowWidth;
		public float FrontGlowAdd => frontGlowAdd;
		public float PatternFilledBoost => patternFilledBoost;
		public float PatternGlow => patternGlow;
		public float SparkSpacing => sparkSpacing;
		public float SparkSize => sparkSize;
		public float SparkJitter => sparkJitter;
		public float SparkSpeed => sparkSpeed;
		public float SparkTwinkleSpeed => sparkTwinkleSpeed;
		public float SparkStrength => sparkStrength;
		public float AddStrength => addStrength;
		public AnimationCurve FillCurve => fillCurve;
		public float AppearSeconds => appearSeconds;
		public float AppearStartScale => appearStartScale;
		public float FlashSeconds => flashSeconds;
		public float FlashStrength => flashStrength;
		public float FlashEdgeBoost => flashEdgeBoost;
		public float ShockDistance => shockDistance;
		public float ShockWidth => shockWidth;
		public bool PlaySfx => playSfx;
	}
}
