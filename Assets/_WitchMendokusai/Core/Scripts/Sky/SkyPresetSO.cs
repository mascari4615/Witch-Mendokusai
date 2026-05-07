using UnityEngine;

namespace WitchMendokusai
{
	// 시간대별 하늘 색·곡선 Preset. 모든 색 = Gradient (0~1 = 00:00~24:00).
	// 사용자가 인스펙터에서 Play 중 점 끌어 색 변경 → SkyDirector 가 매 tick 다시 평가 → 즉시 반영.
	// preset swap = SkyDirector.ActivePreset 인스펙터에서 다른 SO 로 교체.
	// (TASK-WM-054-C C1)
	[CreateAssetMenu(fileName = nameof(SkyPresetSO), menuName = "WM/Sky/SkyPresetSO")]
	public class SkyPresetSO : ScriptableObject
	{
		[field: Header("_" + nameof(SkyPresetSO))]
		[field: Tooltip("preset 이름 (모동숲 / 스타듀 / WM 오리지널 등)")]
		[field: SerializeField] public string DisplayName { get; private set; } = "Animal Crossing";

		[field: Header("Sky Colors (0=00:00, 0.5=12:00, 1=24:00)")]
		[field: SerializeField] public Gradient ZenithColor { get; private set; } = DefaultZenith();
		[field: SerializeField] public Gradient HorizonColor { get; private set; } = DefaultHorizon();
		[field: SerializeField] public Gradient SunDiscColor { get; private set; } = DefaultSunDisc();

		[field: Header("Light / Fog / Ambient")]
		[field: SerializeField] public Gradient SunLightColor { get; private set; } = DefaultSunLight();
		[field: SerializeField] public Gradient FogColor { get; private set; } = DefaultFog();
		[field: SerializeField] public Gradient AmbientColor { get; private set; } = DefaultAmbient();

		[field: Header("Curves (0~1 → scalar)")]
		[field: Tooltip("DirLight intensity (0=꺼짐, 1=정오 강함)")]
		[field: SerializeField] public AnimationCurve SunIntensity { get; private set; } = DefaultSunIntensity();

		[field: Tooltip("해 높이 (0=수평선 / 1=천정 / 다시 0=수평선). 0~1 = 00:00~24:00")]
		[field: SerializeField] public AnimationCurve SunAltitude { get; private set; } = DefaultSunAltitude();

		[field: Tooltip("별 layer alpha (밤만 1)")]
		[field: SerializeField] public AnimationCurve StarAlpha { get; private set; } = DefaultStarAlpha();

		[field: Tooltip("PostFX saturation (-100~100). C3b 에서 사용 (sub-C 후속)")]
		[field: SerializeField] public AnimationCurve PostSaturation { get; private set; } = AnimationCurve.Constant(0f, 1f, 0f);

		// ─── 모동숲 디폴트 색 (사용자 인스펙터 조정 전제) ───

		private static Gradient DefaultZenith()
		{
			return MakeGradient(
				(0.00f, new Color(0.10f, 0.08f, 0.25f)), // 깊은 남보라 (한밤)
				(0.20f, new Color(0.55f, 0.45f, 0.65f)), // dawn 보라
				(0.30f, new Color(0.50f, 0.75f, 0.90f)), // morning cyan
				(0.50f, new Color(0.40f, 0.70f, 0.95f)), // 정오 맑은 파랑
				(0.70f, new Color(0.55f, 0.55f, 0.85f)), // afternoon 톤
				(0.78f, new Color(0.95f, 0.55f, 0.55f)), // sunset 분홍
				(0.85f, new Color(0.30f, 0.20f, 0.55f)), // twilight 보라
				(1.00f, new Color(0.10f, 0.08f, 0.25f))  // 깊은 남보라
			);
		}

		private static Gradient DefaultHorizon()
		{
			return MakeGradient(
				(0.00f, new Color(0.20f, 0.15f, 0.30f)),
				(0.20f, new Color(1.00f, 0.70f, 0.75f)), // dawn 분홍
				(0.30f, new Color(1.00f, 0.95f, 0.85f)), // morning 흰빛
				(0.50f, new Color(0.85f, 0.90f, 1.00f)),
				(0.75f, new Color(1.00f, 0.65f, 0.40f)), // sunset 주황
				(0.83f, new Color(0.85f, 0.45f, 0.55f)),
				(0.90f, new Color(0.25f, 0.20f, 0.40f)),
				(1.00f, new Color(0.20f, 0.15f, 0.30f))
			);
		}

		private static Gradient DefaultSunDisc()
		{
			return MakeGradient(
				(0.00f, new Color(0f, 0f, 0f, 0f)),
				(0.22f, new Color(1.00f, 0.80f, 0.70f, 1f)), // dawn
				(0.50f, new Color(1.00f, 0.97f, 0.85f, 1f)), // 정오
				(0.78f, new Color(1.00f, 0.55f, 0.30f, 1f)), // sunset
				(0.85f, new Color(0f, 0f, 0f, 0f)),
				(1.00f, new Color(0f, 0f, 0f, 0f))
			);
		}

		private static Gradient DefaultSunLight()
		{
			return MakeGradient(
				(0.00f, new Color(0.30f, 0.30f, 0.50f)), // 달빛
				(0.22f, new Color(1.00f, 0.85f, 0.75f)),
				(0.50f, new Color(1.00f, 0.95f, 0.90f)), // 정오 흰
				(0.78f, new Color(1.00f, 0.65f, 0.45f)), // sunset 따뜻
				(0.85f, new Color(0.45f, 0.35f, 0.55f)),
				(1.00f, new Color(0.30f, 0.30f, 0.50f))
			);
		}

		private static Gradient DefaultFog()
		{
			return MakeGradient(
				(0.00f, new Color(0.15f, 0.13f, 0.25f)),
				(0.50f, new Color(0.80f, 0.85f, 0.95f)),
				(0.78f, new Color(0.95f, 0.75f, 0.65f)),
				(1.00f, new Color(0.15f, 0.13f, 0.25f))
			);
		}

		private static Gradient DefaultAmbient()
		{
			return MakeGradient(
				(0.00f, new Color(0.18f, 0.18f, 0.30f)),
				(0.50f, new Color(0.65f, 0.70f, 0.80f)),
				(0.78f, new Color(0.85f, 0.65f, 0.60f)),
				(1.00f, new Color(0.18f, 0.18f, 0.30f))
			);
		}

		private static AnimationCurve DefaultSunIntensity()
		{
			AnimationCurve curve = new AnimationCurve();
			curve.AddKey(0.00f, 0.10f); // 한밤 약한 달빛
			curve.AddKey(0.20f, 0.40f);
			curve.AddKey(0.50f, 1.20f); // 정오 강함
			curve.AddKey(0.78f, 0.70f);
			curve.AddKey(0.88f, 0.15f);
			curve.AddKey(1.00f, 0.10f);
			return curve;
		}

		private static AnimationCurve DefaultSunAltitude()
		{
			AnimationCurve curve = new AnimationCurve();
			curve.AddKey(0.00f, -0.30f); // 한밤 수평선 아래
			curve.AddKey(0.25f, 0.00f);  // dawn 수평선
			curve.AddKey(0.50f, 1.00f);  // 정오 천정
			curve.AddKey(0.75f, 0.00f);  // sunset 수평선
			curve.AddKey(1.00f, -0.30f);
			return curve;
		}

		private static AnimationCurve DefaultStarAlpha()
		{
			AnimationCurve curve = new AnimationCurve();
			curve.AddKey(0.00f, 1.00f); // 한밤 별 뚜렷
			curve.AddKey(0.20f, 0.20f);
			curve.AddKey(0.30f, 0.00f); // morning 별 사라짐
			curve.AddKey(0.78f, 0.00f);
			curve.AddKey(0.88f, 0.50f); // 밤 별 등장
			curve.AddKey(1.00f, 1.00f);
			return curve;
		}

		private static Gradient MakeGradient(params (float time, Color color)[] keys)
		{
			Gradient gradient = new Gradient();
			GradientColorKey[] colorKeys = new GradientColorKey[keys.Length];
			GradientAlphaKey[] alphaKeys = new GradientAlphaKey[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);
				alphaKeys[i] = new GradientAlphaKey(keys[i].color.a, keys[i].time);
			}
			gradient.SetKeys(colorKeys, alphaKeys);
			return gradient;
		}
	}
}
