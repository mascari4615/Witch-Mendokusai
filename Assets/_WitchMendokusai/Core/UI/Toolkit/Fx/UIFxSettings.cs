using UnityEngine;

namespace WitchMendokusai
{
	// UI 연출 고르기와 수치 한 곳. Resources/UIFxSettings.asset, 실행 중 인스펙터에서 조절
	// 무늬와 움직임은 드롭다운 하나로 교체. 우클릭 메뉴 Preview 로 Play 중 바로 확인
	[CreateAssetMenu(fileName = "UIFxSettings", menuName = "WM/UI/UI Fx Settings")]
	public sealed class UIFxSettings : ScriptableObject
	{
		private const string RESOURCE = "UIFxSettings";
		private static UIFxSettings instance;

		/// <summary>인스펙터에서 값이 바뀔 때마다 증가. 계산해 둔 무늬를 다시 만들 때 비교</summary>
		public static int Version { get; private set; }

		private void OnValidate() => Version++;

		public static UIFxSettings Instance
		{
			get
			{
				if (instance == null)
				{
					instance = Resources.Load<UIFxSettings>(RESOURCE);
					Debug.Assert(instance != null, $"Resources/{RESOURCE}.asset 없음");
				}
				return instance;
			}
		}

		[Header("화면 전환 (씬 교체, 포털)")]
		[Tooltip("씬 교체 (타이틀, 로비, 로딩, 월드)")]
		public ScreenWipeStyle sceneWipe = ScreenWipeStyle.Diamond;
		[Tooltip("씬 안 이동 (포털, 던전 입장과 귀환)")]
		public ScreenWipeStyle portalWipe = ScreenWipeStyle.Diamond;
		[Tooltip("Random 이 고르는 후보. 비우면 Fade 제외 전부")]
		public ScreenWipeStyle[] randomPool = new ScreenWipeStyle[0];
		public Color wipeColor = new(0.10f, 0.08f, 0.16f, 1f);
		[Tooltip("칸 전환 (Diamond, Dots, Pixels, Blinds) 한 칸 크기, 기준 해상도 픽셀")]
		public float wipeCellSize = 72f;
		[Tooltip("대각선 끝 칸이 첫 칸보다 늦게 시작하는 비율. 0 이면 동시")]
		[Range(0f, 3f)] public float wipeSpread = 1.2f;
		public float wipeCoverSeconds = 0.55f;
		public float wipeRevealSeconds = 0.6f;
		[Tooltip("다 자란 마름모 배율. 루트 2 (1.414) 이상이어야 칸 사이 틈이 안 보임")]
		public float wipeTileGrowth = 1.5f;
		[Tooltip("무늬 전환 (Iris 이후) 의 계산 해상도 가로 칸 수. 클수록 경계가 또렷하고 무거움")]
		public int wipeMapWidth = 256;
		[Tooltip("무늬 경계 번짐 폭, 진행 비율")]
		[Range(0.01f, 0.5f)] public float wipeEdgeSoftness = 0.07f;
		[Tooltip("무늬 경계에 도는 빛 색")]
		public Color wipeEdgeColor = new(1f, 0.82f, 0.5f, 1f);
		[Range(0f, 1f)] public float wipeEdgeGlow = 0.85f;
		[Tooltip("Spiral 감김 수")]
		public float wipeSpiralTurns = 2f;
		[Tooltip("Fan 날개 수, Star 꼭짓점 수")]
		public int wipeFanBlades = 8;
		public int wipeStarPoints = 5;
		[Tooltip("Crystal 조각 수")]
		public int wipeCrystalCells = 28;
		[Tooltip("Stripes 줄 수, Wave 물결 수")]
		public int wipeStripeCount = 9;
		public float wipeWaveCount = 3f;
		[Tooltip("Wave 물결 높이, 화면 높이 비율")]
		public float wipeWaveAmplitude = 0.07f;
		[Tooltip("Star 꼭짓점 깊이 0~1")]
		[Range(0f, 0.9f)] public float wipeStarDepth = 0.35f;
		[Tooltip("Dissolve 구름 크기. 클수록 잘게")]
		public float wipeDissolveScale = 5f;
		[Tooltip("Ripple 물결 높이와 촘촘함")]
		public float wipeRippleAmplitude = 0.035f;
		public float wipeRippleFrequency = 48f;
		[Tooltip("Halftone 가로 망점 수와 망점 결 세기 (0 이면 매끈한 대각선)")]
		public int wipeHalftoneCells = 16;
		[Range(0f, 1f)] public float wipeHalftoneDotWeight = 0.4f;

		[Header("층 전환 (Shutter, Slash, Glitch, Tube)")]
		[Tooltip("띠 앞머리와 사선의 강조색")]
		public Color wipeAccentColor = new(1f, 0.55f, 0.75f, 1f);
		[Tooltip("강조선 두께, 픽셀")]
		public float wipeAccentLine = 6f;
		public Color wipeFlashColor = Color.white;
		[Range(0f, 1f)] public float wipeFlashAlpha = 0.85f;
		public int wipeShutterBands = 6;
		public float wipeShutterAngle = 16f;
		[Tooltip("첫 띠와 끝 띠의 시작 간격, 전체 시간 비율")]
		[Range(0f, 0.9f)] public float wipeShutterStagger = 0.45f;
		[Tooltip("홀수 띠가 강조색 쪽으로 물드는 정도")]
		[Range(0f, 1f)] public float wipeShutterTint = 0.18f;
		public float wipeSlashAngle = 24f;
		public int wipeGlitchSlices = 18;
		[Tooltip("조각이 어긋나는 최대 거리, 화면 너비 비율")]
		public float wipeGlitchShift = 0.12f;
		[Tooltip("색 갈라짐 거리, 픽셀")]
		public float wipeGlitchSplit = 14f;
		[Tooltip("색 갈라짐이 튀는 횟수 (전환 한 번)")]
		public float wipeGlitchSteps = 14f;
		public Color wipeGlitchWarm = new(1f, 0.2f, 0.45f, 0.85f);
		public Color wipeGlitchCold = new(0.2f, 0.9f, 1f, 0.85f);
		[Tooltip("Tube 가 닫힌 뒤 남는 빛줄 두께, 픽셀")]
		public float wipeTubeLine = 4f;

		[Header("창과 패널 움직임")]
		public UIMotionStyle windowMotion = UIMotionStyle.Pop;
		public UIMotionStyle panelMotion = UIMotionStyle.Rise;

		[Header("창 열기 겉면 빛")]
		public ShineStyle shineStyle = ShineStyle.Sweep;
		[Tooltip("빛 띠와 테두리 번쩍임 색")]
		public Color shineColor = Color.white;
		public float shineDelaySeconds = 0.06f;
		public float shineSeconds = 0.42f;
		[Range(0f, 1f)] public float shineAlpha = 0.16f;
		[Tooltip("빛 띠 너비, 창 너비 대비")]
		[Range(0.05f, 1f)] public float shineWidthRatio = 0.45f;
		public float shineAngle = 18f;
		[Tooltip("기울인 빛 띠 길이, 창 높이 대비. 모서리까지 덮도록 1 보다 크게")]
		public float shineBandLength = 2.2f;
		[Tooltip("EdgeFlash 테두리 두께, 픽셀")]
		public float shineEdgeWidth = 3f;

		[Header("반짝이 입자 (창 열기)")]
		public ParticleShape sparkleShape = ParticleShape.Sparkle;
		public BurstMotion sparkleMotion = BurstMotion.PopRise;
		public int sparkleCount = 16;
		public float sparkleLifeSeconds = 0.9f;
		public Vector2 sparkleSizeRange = new(26f, 46f);
		[Tooltip("위로 떠오르는 속도, 픽셀/초")]
		public float sparkleRiseSpeed = 28f;
		public Color sparkleColor = new(1f, 0.96f, 0.82f, 1f);
		[Tooltip("태어날 때 바깥으로 튀는 세기, 입자 크기 배수")]
		public float sparklePopOut = 2.5f;
		[Tooltip("Fountain 이 솟는 높이, 창 높이 대비")]
		public float sparkleFountainHeight = 0.45f;
		[Tooltip("Orbit 이 도는 속도, 초당 둘레 비율")]
		public float sparkleOrbitSpeed = 0.35f;
		[Tooltip("Fall 낙하 속도, 픽셀/초")]
		public float sparkleFallSpeed = 110f;
		[Tooltip("모난 모양 (별, 꽃잎 등) 의 회전 속도 상한, 도/초")]
		public float particleSpinSpeed = 160f;

		[Header("떠다니는 빛 입자 (전체 패널 뒤)")]
		public bool ambientMotes = true;
		public ParticleShape moteShape = ParticleShape.Glow;
		public MoteMotion moteMotion = MoteMotion.Rise;
		public int moteCount = 28;
		public Vector2 moteSizeRange = new(20f, 44f);
		public float moteRiseSpeed = 22f;
		[Range(0f, 1f)] public float moteAlpha = 0.9f;
		public Color moteColor = new(1f, 0.93f, 0.75f, 1f);

		[Header("평상시 요소 효과 (가만히 있을 때 버튼, 칸, 아이콘에 가끔)")]
		public IdleFxStyle idleFx = IdleFxStyle.Mixed;
		[Tooltip("효과 사이 간격 범위, 초. 패널마다 따로")]
		public Vector2 idleIntervalRange = new(1.6f, 3.6f);
		public Color idleColor = new(1f, 0.95f, 0.8f, 1f);
		[Tooltip("이보다 작은 요소는 대상에서 제외, 픽셀")]
		public float idleMinTargetSize = 28f;
		[Tooltip("패널 넓이 대비 이보다 큰 요소는 제외 (배경, 큰 판)")]
		[Range(0.01f, 1f)] public float idleMaxTargetAreaRatio = 0.2f;
		public float idleGlintSeconds = 0.55f;
		[Tooltip("Mixed 가 고르는 후보. 같은 항목을 여러 번 넣으면 더 자주")]
		public IdleFxStyle[] idleMixedPool =
		{
			IdleFxStyle.Glint, IdleFxStyle.Glint, IdleFxStyle.Twinkle, IdleFxStyle.Twinkle,
			IdleFxStyle.Pulse, IdleFxStyle.Ring, IdleFxStyle.Wave,
		};
		[Range(0f, 1f)] public float idleGlintAlpha = 0.55f;
		[Tooltip("빛 띠 너비, 요소의 긴 변 대비")]
		public float idleGlintWidthRatio = 0.35f;
		public float idleGlintAngle = 18f;
		[Tooltip("기울인 빛 띠 길이, 요소 높이 대비. 모서리까지 덮도록 1 보다 크게")]
		public float idleGlintLength = 1.6f;
		public float idleTwinkleSeconds = 0.75f;
		public float idleTwinkleSize = 44f;
		[Tooltip("반짝임 크기 배율 범위")]
		public Vector2 idleTwinkleSizeJitter = new(0.8f, 1.2f);
		[Tooltip("반짝임 자리, 모서리에서 안쪽으로 변 길이 비율 범위")]
		public Vector2 idleTwinkleCornerRange = new(0.05f, 0.3f);
		[Tooltip("반짝임이 피는 동안 도는 각도")]
		public float idleTwinkleTurnDegrees = 90f;
		public float idleOutlineWidth = 2f;
		public float idlePulseSeconds = 0.9f;
		[Range(0f, 1f)] public float idlePulseFill = 0.08f;
		public float idleRingSeconds = 0.8f;
		[Tooltip("Ring 이 퍼지는 배율")]
		public float idleRingGrowth = 1.25f;
		[Tooltip("Wave 가 훑는 칸 수 상한과 칸 사이 간격")]
		public int idleWaveMaxItems = 8;
		public float idleWaveStepSeconds = 0.07f;

		[Header("자식 순차 등장 (전체 패널)")]
		public bool staggerChildren = true;
		public float staggerStepSeconds = 0.04f;
		public float staggerItemSeconds = 0.22f;
		public float staggerRisePixels = 14f;
		[Tooltip("창 안 격자 칸 (인벤토리 등) 물결 간격")]
		public float gridStaggerStepSeconds = 0.012f;
		[Tooltip("이 수 이상 칸을 가진 자손 중 가장 큰 것을 격자로 봄")]
		public int gridStaggerMinItems = 4;

		[ContextMenu("Preview/Scene Wipe")]
		private void PreviewSceneWipe() => SceneFader.PreviewWipe(sceneWipe);

		[ContextMenu("Preview/Portal Wipe")]
		private void PreviewPortalWipe() => SceneFader.PreviewWipe(portalWipe);

		[ContextMenu("Preview/All Wipes")]
		private void PreviewAllWipes() => SceneFader.PreviewAllWipes();
	}
}
