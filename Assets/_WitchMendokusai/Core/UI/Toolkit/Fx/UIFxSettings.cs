using UnityEngine;

namespace WitchMendokusai
{
	// UI 연출 기본 칸과 공용 수치. Resources/UIFx_1_Settings.asset, 실행 중 인스펙터에서 조절
	// 화면 전환은 UIWipe 에셋, 패널과 창 연출은 UIFxProfile 에셋을 칸에 끼워 교체. 전환 에셋 우클릭 Preview 로 Play 중 확인
	[CreateAssetMenu(fileName = "UIFx_1_Settings", menuName = "WM/UI/UI Fx Settings")]
	public sealed class UIFxSettings : ScriptableObject
	{
		private const string RESOURCE = "UIFx_1_Settings";
		private static UIFxSettings instance;

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

		[Header("화면 전환 기본 칸 (UIWipe 에셋, Presets 폴더)")]
		[Tooltip("씬 교체 (타이틀, 로비, 로딩, 월드). SceneFader.LoadScene 이 에셋을 넘기면 그것")]
		public UIWipe sceneWipe;
		[Tooltip("씬 안 이동 (포털, 던전 입장과 귀환). Portal 의 wipe 칸이 차 있으면 그것")]
		public UIWipe portalWipe;
		[Tooltip("우클릭 Preview/All Wipes 가 차례로 보일 목록")]
		public UIWipe[] previewWipes = new UIWipe[0];

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

		[Header("업적 달성 띠")]
		[Tooltip("띠가 떠 있는 시간 (초). 사라지는 움직임은 USS wm-ach-toast 의 transition")]
		public float achievementToastHoldSeconds = 3f;
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

		[Header("기본 프로필 (UIFxProfile 에셋). 패널과 창이 따로 끼우면 그것")]
		public UIFxProfile panelProfile;
		public UIFxProfile windowProfile;

		[Header("평상시 요소 효과 공용 수치 (효과 모양은 프로필의 idleEffect)")]
		[Tooltip("효과 사이 간격 범위, 초. 패널마다 따로")]
		public Vector2 idleIntervalRange = new(1.6f, 3.6f);
		[Tooltip("이보다 작은 요소는 대상에서 제외, 픽셀")]
		public float idleMinTargetSize = 28f;
		[Tooltip("패널 넓이 대비 이보다 큰 요소는 제외 (배경, 큰 판)")]
		[Range(0.01f, 1f)] public float idleMaxTargetAreaRatio = 0.2f;
		[Tooltip("이보다 흐린 조상이 있으면 숨은 것으로 봄 (닫힘 애니메이션 끝 무렵 포함)")]
		[Range(0f, 1f)] public float idleVisibleOpacity = 0.5f;
		[Tooltip("대상이 효과 도중 사라져 완료가 안 오면 이 시간 뒤 다음 효과로")]
		public float idleBusyTimeoutSeconds = 4f;

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
		private void PreviewAllWipes() => SceneFader.PreviewWipes(previewWipes);
	}
}
