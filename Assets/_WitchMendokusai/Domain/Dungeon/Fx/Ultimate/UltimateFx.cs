using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 스킬 (궁극기) 연출 한 벌. 에셋 하나가 연출 하나, 새 궁극기는 에셋을 복제해 값만 바꿈
	/// 구성: 예고 (바닥 마법진) -> 충전 (빨려드는 빛, 시네마 바, 줌인, 떨림) -> 임팩트 (히트스톱, 섬광, 줌 킥, 흔들림, 속도선,
	/// 충격파 굴절 링, 지면 균열, 먼지와 파편, 빛기둥, 임시 광원, 색 번짐) -> 환경 반응 (풀과 나무가 충격파에 휘어짐) -> 잔여 (식는 균열, 불씨, 연기)
	/// - 값은 이 에셋의 직렬화 필드만. 진행 코드는 UltimateFxRun
	/// - 수치 근거는 memo wm/design/fx-ceiling-20261119 (영상 프레임 실측)
	/// - 예고 칸에는 기존 바닥 예고 부품 (GroundTelegraph) 을 끼움. 비우면 마법진 판만
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_0_Sample", menuName = "WM/CombatFx/" + nameof(UltimateFx))]
	public sealed class UltimateFx : CombatFx
	{
		[Serializable]
		public struct FoliageKind
		{
			public GameObject Prefab;
			[Min(0)] public int Count;
			[Range(0f, 1f)] public float Flexibility;
			public Vector2 ScaleRange;
			[Min(0f), Tooltip("중심에서 이만큼 안쪽에는 놓지 않음")] public float MinDistance;
		}

		[Serializable]
		public struct Palette
		{
			[ColorUsage(false, true), Tooltip("가장 뜨거운 속 (섬광, 균열 속)")] public Color Core;
			[ColorUsage(false, true), Tooltip("주 색 (충격파, 불꽃)")] public Color Mid;
			[ColorUsage(false, true), Tooltip("식은 색 (균열 끝, 후폭풍)")] public Color Deep;
			[Tooltip("화면 가장자리 어둠")] public Color Shade;
			[Tooltip("먼지, 연기")] public Color Dust;
			[Tooltip("파편")] public Color Rock;
		}

		[Header("연결")]
		[SerializeField] private Shader spriteShader;
		[SerializeField] private Shader shockwaveShader;
		[SerializeField, Tooltip("예고 때 쓸 바닥 예고 부품. 비워도 됨")] private CombatFx telegraph;
		[SerializeField] private Palette palette = new()
		{
			Core = new Color(1.6f, 1.45f, 1.1f),
			Mid = new Color(1.5f, 0.75f, 0.2f),
			Deep = new Color(0.8f, 0.12f, 0.04f),
			Shade = new Color(0.05f, 0.01f, 0.01f),
			Dust = new Color(0.55f, 0.43f, 0.32f, 0.55f),
			Rock = new Color(0.62f, 0.5f, 0.42f),
		};

		[Header("시간 (초)")]
		[SerializeField, Min(0f)] private float telegraphSeconds = 0.55f;
		[SerializeField, Min(0.1f)] private float chargeSeconds = 1.15f;
		[SerializeField, Min(0.5f), Tooltip("임팩트 뒤 입자가 다 사라질 때까지 붙드는 시간")] private float aftermathSeconds = 7.5f;

		[Header("범위")]
		[SerializeField, Min(1f), Tooltip("충격파, 균열, 마법진 반지름 (월드 단위)")] private float impactRadius = 7f;

		[Header("충전")]
		[SerializeField, Range(0f, 1f)] private float chargeDarken = 0.5f;
		[SerializeField, Range(0f, 0.3f), Tooltip("시네마 바 높이 (화면 비율)")] private float barRatio = 0.12f;
		[SerializeField, Range(0.4f, 1f)] private float chargeZoomScale = 0.78f;
		[SerializeField] private float chargeFieldOfView = -7f;
		[SerializeField, Min(0)] private int chargeSparksPerSecond = 120;
		[SerializeField, Min(1f)] private float chargeSparkRadius = 9f;
		[SerializeField, Min(0f)] private float chargeTremor = 0.3f;

		[Header("임팩트")]
		[SerializeField, Min(0f), Tooltip("실제 시간 초")] private float hitStopSeconds = 0.14f;
		[SerializeField, Range(0.001f, 1f)] private float hitStopScale = 0.02f;
		[SerializeField, Range(0f, 1.5f), Tooltip("섬광 정점 알파. 1 이면 화면 전체가 흰색")] private float flashPeak = 0.94f;
		[SerializeField, Min(0f)] private float flashHoldSeconds = 0.07f;
		[SerializeField, Min(0.05f)] private float flashDecaySeconds = 0.55f;
		[SerializeField, Range(0f, 1f), Tooltip("임팩트 뒤 화면을 덮는 색 번짐 정점")] private float washPeak = 0.22f;
		[SerializeField, Min(0.1f)] private float washSeconds = 1.8f;
		[SerializeField, Range(0f, 1f)] private float impactVignette = 0.6f;
		[SerializeField, Min(0f)] private float shakeAmplitude = 1.6f;
		[SerializeField, Min(1)] private int shakeCount = 8;
		[SerializeField, Min(0.01f)] private float shakeInterval = 0.06f;
		[SerializeField, Range(0.5f, 1f)] private float shakeDecay = 0.78f;
		[SerializeField, Range(1f, 1.5f), Tooltip("임팩트 순간 카메라가 밀려나는 거리 배율")] private float kickDistanceScale = 1.16f;
		[SerializeField] private float kickFieldOfView = 8f;
		[SerializeField, Min(0f)] private float streakSeconds = 0.5f;
		[SerializeField, Range(0f, 1f)] private float streakAlpha = 0.85f;

		[Header("충격파")]
		[SerializeField, Range(1, 4)] private int ringCount = 3;
		[SerializeField, Min(0.05f)] private float ringDelay = 0.12f;
		[SerializeField, Min(0.2f)] private float ringSeconds = 0.95f;
		[SerializeField, Min(1f), Tooltip("고리 최대 반지름이 충격 반지름의 몇 배인가")] private float ringReach = 1.5f;
		[SerializeField, Min(0f), Tooltip("굴절 세기 (화면 비율)")] private float distortion = 0.055f;
		[SerializeField, Range(0.02f, 0.4f)] private float ringWidth = 0.16f;

		[Header("지면")]
		[SerializeField] private int crackSeed = 7;
		[SerializeField, Min(0.5f)] private float crackRevealSeconds = 0.22f;
		[SerializeField, Min(0.5f)] private float crackHeatSeconds = 4f;
		[SerializeField, Min(0f)] private int dustCount = 150;
		[SerializeField, Min(0)] private int debrisCount = 56;
		[SerializeField, Min(0)] private int sparkCount = 140;
		[SerializeField, Min(1f)] private float debrisSpeed = 15f;

		[Header("빛기둥과 광원")]
		[SerializeField, Min(1f)] private float pillarHeight = 22f;
		[SerializeField, Min(0.5f)] private float pillarWidth = 4.2f;
		[SerializeField, Min(0.1f)] private float pillarSeconds = 0.85f;
		[SerializeField, Min(0f)] private float lightIntensity = 40f;
		[SerializeField, Min(1f)] private float lightRange = 24f;
		[SerializeField, Min(0.1f)] private float lightSeconds = 2.2f;

		[Header("환경 반응")]
		[SerializeField, Min(0f), Tooltip("풀과 나무 최대 기울기 (도)")] private float foliageBend = 62f;
		[SerializeField, Min(0.1f)] private float foliageDecaySeconds = 1.1f;
		[SerializeField, Min(0.5f)] private float foliageHertz = 1.9f;
		[SerializeField, Tooltip("비어 있으면 시연용 임시 풀과 나무를 깔고, 끝나면 치움 (진열장 용)")] private bool spawnTemporaryFoliage = true;
		[SerializeField] private FoliageKind[] foliage = Array.Empty<FoliageKind>();

		[Header("잔여")]
		[SerializeField, Min(0)] private int emberCount = 90;
		[SerializeField, Min(0)] private int smokeCount = 26;

		public Shader SpriteShader => spriteShader;
		public Shader ShockwaveShader => shockwaveShader;
		public CombatFx Telegraph => telegraph;
		public Palette Colors => palette;
		public float TelegraphSeconds => telegraphSeconds;
		public float ChargeSeconds => chargeSeconds;
		public float AftermathSeconds => aftermathSeconds;
		public float ImpactRadius => impactRadius;
		public float ChargeDarken => chargeDarken;
		public float BarRatio => barRatio;
		public float ChargeZoomScale => chargeZoomScale;
		public float ChargeFieldOfView => chargeFieldOfView;
		public int ChargeSparksPerSecond => chargeSparksPerSecond;
		public float ChargeSparkRadius => chargeSparkRadius;
		public float ChargeTremor => chargeTremor;
		public float HitStopSeconds => hitStopSeconds;
		public float HitStopScale => hitStopScale;
		public float FlashPeak => flashPeak;
		public float FlashHoldSeconds => flashHoldSeconds;
		public float FlashDecaySeconds => flashDecaySeconds;
		public float WashPeak => washPeak;
		public float WashSeconds => washSeconds;
		public float ImpactVignette => impactVignette;
		public float ShakeAmplitude => shakeAmplitude;
		public int ShakeCount => shakeCount;
		public float ShakeInterval => shakeInterval;
		public float ShakeDecay => shakeDecay;
		public float KickDistanceScale => kickDistanceScale;
		public float KickFieldOfView => kickFieldOfView;
		public float StreakSeconds => streakSeconds;
		public float StreakAlpha => streakAlpha;
		public int RingCount => ringCount;
		public float RingDelay => ringDelay;
		public float RingSeconds => ringSeconds;
		public float RingReach => ringReach;
		public float Distortion => distortion;
		public float RingWidth => ringWidth;
		public int CrackSeed => crackSeed;
		public float CrackRevealSeconds => crackRevealSeconds;
		public float CrackHeatSeconds => crackHeatSeconds;
		public int DustCount => dustCount;
		public int DebrisCount => debrisCount;
		public int SparkCount => sparkCount;
		public float DebrisSpeed => debrisSpeed;
		public float PillarHeight => pillarHeight;
		public float PillarWidth => pillarWidth;
		public float PillarSeconds => pillarSeconds;
		public float LightIntensity => lightIntensity;
		public float LightRange => lightRange;
		public float LightSeconds => lightSeconds;
		public float FoliageBend => foliageBend;
		public float FoliageDecaySeconds => foliageDecaySeconds;
		public float FoliageHertz => foliageHertz;
		public bool SpawnTemporaryFoliage => spawnTemporaryFoliage;
		public FoliageKind[] Foliage => foliage;
		public int EmberCount => emberCount;
		public int SmokeCount => smokeCount;

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			UltimateFxRun run = new(this, context);
			return runner.Run(run.Routine(), run.Dispose);
		}
	}
}
