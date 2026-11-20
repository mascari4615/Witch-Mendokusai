using UnityEngine;
using Pulse = WitchMendokusai.UltimateScreenFeel.Pulse;
using Beat = WitchMendokusai.UltimateScreenFeel.Beat;
using Swell = WitchMendokusai.UltimateScreenFeel.Swell;
using Kind = WitchMendokusai.UltimateScreenFeel.ImpactFrameKind;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 판 V2 넷의 화면 연출 박자표 처음 값. 생성기가 에셋이 없을 때 (또는 다시 넣기 메뉴) 한 번 넣음. 그 뒤 정본은 에셋
	/// - 연출 시간 (FxTime, FxStart, FxEnd) 은 body (V1 강한 판 복사) 의 시간선 기준. 정지 한 박은 닿기 0.005초 전에 둠 (V1 의 한 장 효과가 정지 동안 늘어나지 않게)
	/// - 넷이 서로 다른 박자: 하늘 검 짧고 무거운 한 방, 창 왕관 쌓이는 연타, 카드 화려한 원진과 마지막 한 장의 대조, 달 느리고 장엄한 절단과 여운
	/// - 섬광 (노출, 빛 번짐) 은 처음 값의 0.7배. 독립 검토 넷이 눈부심을 지적 (memo wm/design/ult3d-feel-v2-20261120)
	/// - 수치 근거: memo wm/design/ult-feel-20261120 (흔들림 0.73~3.19% 폭, 펀치 줌 2.2~6.1% 를 67ms 에, 정지 50~280ms, 섬광 1.46배 반치 133ms, 방사 흐림 0.15~0.30)
	/// </summary>
	public static class UltimateFeelV2Seeds
	{
		private static Pulse P(float amount, float attack, float hold, float halfLife) => new(amount, attack, hold, halfLife);

		/// <summary> 느리게 머물다 끝에서 빨리 풀림 </summary>
		private static AnimationCurve LateRelease() => new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.6f, 0.25f, 1f, 1f), new Keyframe(1f, 1f, 3f, 0f));

		/// <summary> 처음에 빨리 풀리고 끝이 길게 </summary>
		private static AnimationCurve EarlyRelease() => new(new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0.2f, 0f));

		private static AnimationCurve RiseLate() => new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.4f, 0f));

		private static AnimationCurve RiseSteady() => new(new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(1f, 1f, 1.4f, 0f));

		public static UltimateScreenFeel.TraumaSettings Trauma(float maxOffset, float frequency, float decay, float vertical) => new()
		{
			MaxOffset = maxOffset,
			MaxRoll = 1.2f,
			Frequency = frequency,
			DecayPerSecond = decay,
			VerticalBias = vertical,
			Response = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f)),
		};

		// ---- 하늘 검: 떠 있는 동안 어둡게 당기고, 닿기 직전 정지 0.15초, 한 방에 전부 ----

		public static Swell[] SkySwordSwells() => new[]
		{
			new Swell { Label = "검이 떠 있음", FxStart = 0.9f, FxEnd = 1.745f, ReleaseSeconds = 0.18f, Shape = RiseLate(), TimeScale = 0.75f, Exposure = -0.8f, Saturation = -30f, Contrast = 12f, Vignette = 0.3f, Zoom = 0.08f, Dolly = 0.03f, Rumble = 0.14f },
		};

		public static Beat[] SkySwordBeats() => new[]
		{
			new Beat
			{
				Label = "접지", FxTime = 1.745f, HoldSeconds = 0.15f, HoldTimeScale = 0f,
				SlowScale = 0.28f, SlowSeconds = 0.6f, SlowRecovery = LateRelease(),
				Trauma = 0.95f, Zoom = P(0.1f, 0.05f, 0.08f, 0.2f), Dolly = P(0.07f, 0.05f, 0.06f, 0.22f),
				Exposure = P(1.05f, 0.02f, 0.05f, 0.13f), Bloom = P(2.1f, 0.03f, 0.1f, 0.3f), Saturation = P(35f, 0.05f, 0.2f, 0.4f), Contrast = P(25f, 0.03f, 0.12f, 0.3f),
				LensDistortion = P(-0.35f, 0.04f, 0.04f, 0.16f), Vignette = P(0.12f, 0.05f, 0.1f, 0.3f),
				RadialBlur = P(0.85f, 0.02f, 0.06f, 0.12f), Ripple = P(0.035f, 0.02f, 0f, 0.25f), RippleSpeed = 1.5f,
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.85f,
			},
		};

		// ---- 창 왕관: 창 비 동안 떨림과 작은 타격이 점점 커짐, 왕관에서 정지와 최대, 분해에서 작은 마무리 ----

		public static Swell[] CrownSwells() => new[]
		{
			new Swell { Label = "창 비", FxStart = 0.05f, FxEnd = 0.74f, ReleaseSeconds = 0.25f, Shape = RiseSteady(), TimeScale = 0.9f, Exposure = -0.55f, Saturation = -15f, Contrast = 18f, Vignette = 0.28f, Zoom = 0.05f, Dolly = 0.02f, Rumble = 0.3f },
		};

		public static Beat[] CrownBeats() => new[]
		{
			MiniBeat("창 1", 0.3f, 0.16f, 0.012f, 0.15f, 0f),
			MiniBeat("창 2", 0.44f, 0.24f, 0.018f, 0.25f, 0.12f),
			MiniBeat("창 3", 0.56f, 0.32f, 0.025f, 0.35f, 0.2f),
			MiniBeat("창 4", 0.66f, 0.42f, 0.035f, 0.45f, 0.3f),
			new Beat
			{
				Label = "왕관", FxTime = 0.745f, HoldSeconds = 0.12f, HoldTimeScale = 0f,
				SlowScale = 0.35f, SlowSeconds = 0.5f, SlowRecovery = LateRelease(),
				Trauma = 1f, Zoom = P(0.11f, 0.04f, 0.1f, 0.22f), Dolly = P(0.06f, 0.04f, 0.08f, 0.25f),
				Exposure = P(0.84f, 0.02f, 0.05f, 0.14f), Bloom = P(1.8f, 0.03f, 0.12f, 0.35f), Saturation = P(45f, 0.04f, 0.25f, 0.4f), Contrast = P(20f, 0.03f, 0.1f, 0.3f),
				LensDistortion = P(-0.3f, 0.03f, 0.05f, 0.15f), Vignette = P(0.1f, 0.05f, 0.1f, 0.3f),
				RadialBlur = P(0.7f, 0.02f, 0.06f, 0.14f), Ripple = P(0.04f, 0.02f, 0f, 0.28f), RippleSpeed = 1.7f,
				ImpactFrame = Kind.Silhouette, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.8f,
			},
			new Beat
			{
				Label = "분해", FxTime = 1.6f, SlowScale = 0.6f, SlowSeconds = 0.25f, SlowRecovery = EarlyRelease(),
				Trauma = 0.4f, Zoom = P(0.03f, 0.03f, 0.03f, 0.15f), Exposure = P(0.32f, 0.02f, 0.03f, 0.12f), Bloom = P(0.7f, 0.03f, 0.05f, 0.25f),
				RadialBlur = P(0.35f, 0.02f, 0.03f, 0.1f), Ripple = P(0.02f, 0.02f, 0f, 0.2f), RippleSpeed = 1.3f,
			},
		};

		private static Beat MiniBeat(string label, float fxTime, float trauma, float zoom, float exposure, float radial) => new()
		{
			Label = label, FxTime = fxTime, SlowScale = 1f,
			Trauma = trauma, Zoom = P(zoom, 0.03f, 0.02f, 0.1f), Exposure = P(exposure, 0.015f, 0.02f, 0.08f),
			Bloom = P(exposure * 2f, 0.02f, 0.02f, 0.15f), RadialBlur = P(radial, 0.015f, 0.02f, 0.07f),
		};

		// ---- 카드: 원진 동안 밝고 화려 (채도 올림), 연사 잔타격, 정적 (어둡고 탈색, 느려짐) 뒤 마지막 한 장에서 색이 터짐 ----
		// 임팩트 프레임은 흑백. 반전은 어두운 정적 장면을 뒤집어 회백색 전체 화면 (독립 검토 1차, 2차), 실루엣은 어두운 장면에 밝은 곳이 없어 검은 화면 (3회차 촬영)

		public static Swell[] CardSwells() => new[]
		{
			new Swell { Label = "원진", FxStart = 0f, FxEnd = 1.8f, ReleaseSeconds = 0.3f, Shape = RiseSteady(), TimeScale = 1f, Exposure = 0.25f, Saturation = 30f, Contrast = 10f, Vignette = 0f, Zoom = 0.03f, Dolly = 0f, Rumble = 0f },
			new Swell { Label = "정적", FxStart = 2.2f, FxEnd = 2.695f, ReleaseSeconds = 0.12f, Shape = RiseLate(), TimeScale = 0.45f, Exposure = -1.1f, Saturation = -65f, Contrast = 5f, Vignette = 0.45f, Zoom = 0.1f, Dolly = 0.02f, Rumble = 0f },
		};

		public static Beat[] CardBeats() => new[]
		{
			MiniBeat("연사 1", 1.86f, 0.14f, 0.01f, 0.2f, 0f),
			MiniBeat("연사 2", 1.96f, 0.16f, 0.01f, 0.2f, 0f),
			MiniBeat("연사 3", 2.06f, 0.18f, 0.014f, 0.25f, 0.1f),
			MiniBeat("연사 4", 2.16f, 0.2f, 0.018f, 0.3f, 0.12f),
			new Beat
			{
				Label = "마지막 한 장", FxTime = 2.695f, HoldSeconds = 0.2f, HoldTimeScale = 0f,
				SlowScale = 0.25f, SlowSeconds = 0.7f, SlowRecovery = LateRelease(),
				Trauma = 0.85f, Zoom = P(0.11f, 0.05f, 0.1f, 0.25f), Dolly = P(0.05f, 0.05f, 0.1f, 0.25f),
				Exposure = P(0.9f, 0.02f, 0.06f, 0.14f), Bloom = P(2.1f, 0.03f, 0.12f, 0.35f), Saturation = P(70f, 0.03f, 0.3f, 0.45f), Contrast = P(25f, 0.03f, 0.12f, 0.3f),
				LensDistortion = P(-0.3f, 0.04f, 0.05f, 0.16f), Vignette = P(0.1f, 0.04f, 0.1f, 0.3f),
				RadialBlur = P(0.6f, 0.02f, 0.06f, 0.14f), Ripple = P(0.03f, 0.02f, 0f, 0.25f), RippleSpeed = 1.4f,
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.85f,
			},
			new Beat
			{
				Label = "폭산", FxTime = 3.1f, SlowScale = 0.7f, SlowSeconds = 0.3f, SlowRecovery = EarlyRelease(),
				Trauma = 0.35f, Zoom = P(0.025f, 0.03f, 0.04f, 0.15f), Exposure = P(0.28f, 0.02f, 0.03f, 0.12f), Saturation = P(20f, 0.03f, 0.2f, 0.4f),
				Ripple = P(0.02f, 0.02f, 0f, 0.2f), RippleSpeed = 1.2f,
			},
		};

		// ---- 달: 떠오르는 동안 길게 어두워지며 당김, 검기 직전 정지 0.18초, 검기와 절단을 긴 느려짐 속에서, 여운 1.4초 ----

		public static Swell[] MoonSwells() => new[]
		{
			new Swell { Label = "떠오름", FxStart = 0f, FxEnd = 1.595f, ReleaseSeconds = 0.6f, Shape = RiseSteady(), TimeScale = 0.8f, Exposure = -0.9f, Saturation = -30f, Contrast = 15f, Vignette = 0.35f, Zoom = 0.12f, Dolly = 0.03f, Rumble = 0.1f },
		};

		public static Beat[] MoonBeats() => new[]
		{
			new Beat
			{
				Label = "검기", FxTime = 1.595f, HoldSeconds = 0.18f, HoldTimeScale = 0f,
				SlowScale = 0.35f, SlowSeconds = 0.5f, SlowRecovery = LateRelease(),
				Trauma = 0.35f, Zoom = P(0.05f, 0.04f, 0.1f, 0.3f),
				Exposure = P(0.5f, 0.02f, 0.04f, 0.12f), Bloom = P(1.05f, 0.03f, 0.1f, 0.3f), Contrast = P(20f, 0.03f, 0.15f, 0.35f),
				RadialBlur = P(0.4f, 0.02f, 0.05f, 0.12f),
			},
			new Beat
			{
				Label = "절단", FxTime = 1.76f, SlowScale = 0.3f, SlowSeconds = 1.4f, SlowRecovery = LateRelease(),
				Trauma = 0.8f, Zoom = P(0.08f, 0.06f, 0.2f, 0.45f), Dolly = P(0.05f, 0.06f, 0.2f, 0.5f),
				Exposure = P(0.7f, 0.03f, 0.08f, 0.2f), Bloom = P(1.75f, 0.05f, 0.25f, 0.6f), Saturation = P(35f, 0.05f, 0.4f, 0.6f), Contrast = P(15f, 0.05f, 0.2f, 0.5f),
				LensDistortion = P(-0.25f, 0.06f, 0.1f, 0.3f), Vignette = P(0.15f, 0.1f, 0.3f, 0.6f),
				RadialBlur = P(0.5f, 0.03f, 0.1f, 0.25f), Ripple = P(0.03f, 0.03f, 0f, 0.4f), RippleSpeed = 0.9f,
			},
		};
	}
}
