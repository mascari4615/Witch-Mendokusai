using UnityEngine;
using Pulse = WitchMendokusai.UltimateScreenFeel.Pulse;
using Beat = WitchMendokusai.UltimateScreenFeel.Beat;
using Swell = WitchMendokusai.UltimateScreenFeel.Swell;
using Kind = WitchMendokusai.UltimateScreenFeel.ImpactFrameKind;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 판 V2 배치 3 둘 (별자리, 밤의 날) 의 화면 연출 박자표 처음 값. 생성기가 에셋이 없을 때 (또는 다시 넣기 메뉴) 한 번 넣음. 그 뒤 정본은 에셋
	/// - 연출 시간은 body (Ult3D_&lt;이름&gt;Heavy) 시간선 기준. 정지 한 박은 닿기 0.005초 전. body 시각을 바꾸면 여기도 고쳐야 함 (같은 수치 두 곳, 배치 2 와 같은 공백)
	/// - 앞선 여덟과 다른 박자
	///   별자리: 조용한 점등 아홉 (흔들림 거의 없음) -> 숨 멎음 (가장 느리고 어두움) -> 기둥 한 방 -> 쏟아지는 동안 떨림이 점점 커짐 -> 네 갈래 터짐이 최대. 봉우리 둘, 둘째가 더 큼
	///   밤의 날: 첫 베기는 가장 짧은 정지의 단타 -> 검은 사선의 고요 -> 열림은 바깥으로 밀림 (렌즈 볼록) -> 밤 동안 긴 당김 -> 둘째 베기가 최대이자 낮 복귀 섬광
	/// - 섬광 세기 상한은 배치 2 와 같은 범위. 점등 아홉은 광과민 기준으로 노출 0.1 이하
	/// - 설계와 근거 memo wm/design/ult3d-batch3-20261121, ult-feel-20261120
	/// </summary>
	public static class UltimateFeelV2Batch3Seeds
	{
		private static Pulse P(float amount, float attack, float hold, float halfLife) => new(amount, attack, hold, halfLife);

		private static AnimationCurve LateRelease() => new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.6f, 0.25f, 1f, 1f), new Keyframe(1f, 1f, 3f, 0f));

		private static AnimationCurve EarlyRelease() => new(new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0.2f, 0f));

		private static AnimationCurve RiseLate() => new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.4f, 0f));

		private static AnimationCurve RiseSteady() => new(new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(1f, 1f, 1.4f, 0f));

		private static Beat MiniBeat(string label, float fxTime, float trauma, float zoom, float exposure, float radial) => new()
		{
			Label = label, FxTime = fxTime, SlowScale = 1f,
			Trauma = trauma, Zoom = P(zoom, 0.03f, 0.02f, 0.1f), Exposure = P(exposure, 0.015f, 0.02f, 0.08f),
			Bloom = P(exposure * 2f, 0.02f, 0.02f, 0.15f), RadialBlur = P(radial, 0.015f, 0.02f, 0.07f),
		};

		public static UltimateScreenFeel.PullBackSettings PullBack(float amount, float inSeconds, float outSeconds) => new()
		{
			Amount = amount,
			InSeconds = inSeconds,
			OutSeconds = outSeconds,
			Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
		};

		// ---- 별자리: 점등 아홉 (0.3 부터 0.13 간격) -> 숨 멎음 1.47~1.83 -> 기둥 1.83 -> 쏟아짐 ~3.03 -> 네 갈래 터짐 3.03 ----

		public static Swell[] ConstellationSwells() => new[]
		{
			new Swell { Label = "별자리", FxStart = 0.2f, FxEnd = 1.47f, ReleaseSeconds = 0.1f, Shape = RiseSteady(), TimeScale = 1f, Exposure = -0.35f, Saturation = -10f, Contrast = 8f, Vignette = 0.25f, Zoom = 0.04f, Dolly = 0.01f, Rumble = 0f },
			new Swell { Label = "숨 멎음", FxStart = 1.47f, FxEnd = 1.825f, ReleaseSeconds = 0.08f, Shape = RiseSteady(), TimeScale = 0.5f, Exposure = -0.7f, Saturation = -25f, Contrast = 18f, Vignette = 0.45f, Zoom = 0.1f, Dolly = 0.03f, Rumble = 0f },
			new Swell { Label = "쏟아짐", FxStart = 1.95f, FxEnd = 3.025f, ReleaseSeconds = 0.08f, Shape = RiseLate(), TimeScale = 0.9f, Exposure = 0.1f, Saturation = 15f, Contrast = 10f, Vignette = 0.2f, Zoom = 0.06f, Dolly = 0.02f, Rumble = 0.45f },
		};

		public static Beat[] ConstellationBeats()
		{
			Beat[] beats = new Beat[11];
			for (int i = 0; i < 9; i++)
				beats[i] = MiniBeat("점등 " + (i + 1), 0.3f + i * 0.13f, 0.05f + i * 0.01f, 0.004f, 0.05f + i * 0.005f, 0f);
			beats[9] = new Beat
			{
				Label = "기둥", FxTime = 1.825f, HoldSeconds = 0.16f, HoldTimeScale = 0f,
				SlowScale = 0.45f, SlowSeconds = 0.5f, SlowRecovery = LateRelease(),
				Trauma = 0.8f, Zoom = P(0.07f, 0.04f, 0.06f, 0.2f), Dolly = P(0.04f, 0.04f, 0.05f, 0.2f),
				Exposure = P(0.8f, 0.015f, 0.04f, 0.12f), Bloom = P(1.8f, 0.02f, 0.1f, 0.3f), Saturation = P(30f, 0.04f, 0.2f, 0.35f), Contrast = P(20f, 0.03f, 0.1f, 0.3f),
				LensDistortion = P(-0.25f, 0.03f, 0.04f, 0.15f), Vignette = P(0.1f, 0.04f, 0.1f, 0.3f),
				RadialBlur = P(0.45f, 0.02f, 0.05f, 0.12f), Ripple = P(0.03f, 0.02f, 0f, 0.25f), RippleSpeed = 1.6f,
			};
			beats[10] = new Beat
			{
				Label = "네 갈래 터짐", FxTime = 3.025f, HoldSeconds = 0.1f, HoldTimeScale = 0f,
				SlowScale = 0.3f, SlowSeconds = 0.85f, SlowRecovery = LateRelease(),
				Trauma = 1f, Zoom = P(0.1f, 0.04f, 0.08f, 0.22f), Dolly = P(0.05f, 0.04f, 0.06f, 0.22f),
				Exposure = P(0.9f, 0.02f, 0.05f, 0.14f), Bloom = P(2.1f, 0.03f, 0.12f, 0.35f), Saturation = P(45f, 0.04f, 0.25f, 0.4f), Contrast = P(25f, 0.03f, 0.12f, 0.3f),
				LensDistortion = P(-0.35f, 0.04f, 0.04f, 0.16f), Vignette = P(0.12f, 0.05f, 0.1f, 0.3f),
				RadialBlur = P(0.6f, 0.02f, 0.06f, 0.12f), Ripple = P(0.05f, 0.02f, 0f, 0.3f), RippleSpeed = 2f,
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.85f,
			};
			return beats;
		}

		// ---- 밤의 날: 당김 -> 첫 베기 0.5 (단타) -> 검은 사선 ~0.9 -> 열림 0.9 (밀림) -> 밤 1.3~2.7 (긴 당김) -> 흰 날 2.28 -> 둘째 베기 2.7 (최대, 낮 복귀) ----

		public static Swell[] NightBladeSwells() => new[]
		{
			new Swell { Label = "당김", FxStart = 0.05f, FxEnd = 0.495f, ReleaseSeconds = 0.06f, Shape = RiseLate(), TimeScale = 0.8f, Exposure = -0.3f, Saturation = -10f, Contrast = 10f, Vignette = 0.2f, Zoom = 0.05f, Dolly = 0.02f, Rumble = 0.12f },
			new Swell { Label = "검은 사선", FxStart = 0.56f, FxEnd = 0.9f, ReleaseSeconds = 0.1f, Shape = RiseSteady(), TimeScale = 0.6f, Exposure = -0.5f, Saturation = -30f, Contrast = 15f, Vignette = 0.3f, Zoom = 0.03f, Dolly = 0f, Rumble = 0f },
			new Swell { Label = "밤", FxStart = 1.3f, FxEnd = 2.695f, ReleaseSeconds = 0.08f, Shape = RiseLate(), TimeScale = 0.7f, Exposure = -0.45f, Saturation = -10f, Contrast = 15f, Vignette = 0.45f, Zoom = 0.12f, Dolly = 0.04f, Rumble = 0.3f },
		};

		public static Beat[] NightBladeBeats() => new[]
		{
			new Beat
			{
				Label = "첫 베기", FxTime = 0.495f, HoldSeconds = 0.06f, HoldTimeScale = 0f,
				SlowScale = 0.5f, SlowSeconds = 0.25f, SlowRecovery = EarlyRelease(),
				Trauma = 0.6f, Zoom = P(0.04f, 0.03f, 0.04f, 0.15f),
				Exposure = P(0.5f, 0.015f, 0.03f, 0.1f), Bloom = P(1f, 0.02f, 0.06f, 0.25f), Chromatic = P(0.2f, 0.02f, 0.04f, 0.12f),
				LensDistortion = P(-0.15f, 0.03f, 0.04f, 0.15f), RadialBlur = P(0.35f, 0.02f, 0.04f, 0.1f),
				ImpactFrame = Kind.Silhouette, ImpactFrameSeconds = 0.042f, ImpactFrameStrength = 0.8f,
			},
			new Beat
			{
				Label = "열림", FxTime = 0.9f, SlowScale = 0.7f, SlowSeconds = 0.4f, SlowRecovery = EarlyRelease(),
				Trauma = 0.35f, Zoom = P(-0.03f, 0.05f, 0.1f, 0.3f), Bloom = P(0.6f, 0.05f, 0.1f, 0.3f),
				LensDistortion = P(0.2f, 0.05f, 0.1f, 0.25f), Ripple = P(0.04f, 0.03f, 0f, 0.4f), RippleSpeed = 1.2f,
			},
			MiniBeat("흰 날", 2.28f, 0.25f, 0.015f, 0.3f, 0.15f),
			new Beat
			{
				Label = "둘째 베기", FxTime = 2.695f, HoldSeconds = 0.2f, HoldTimeScale = 0f,
				SlowScale = 0.3f, SlowSeconds = 0.9f, SlowRecovery = LateRelease(),
				Trauma = 1f, Zoom = P(0.1f, 0.04f, 0.08f, 0.22f), Dolly = P(0.05f, 0.04f, 0.06f, 0.22f),
				Exposure = P(1f, 0.02f, 0.05f, 0.14f), Bloom = P(2.2f, 0.03f, 0.12f, 0.35f), Saturation = P(50f, 0.04f, 0.3f, 0.45f), Contrast = P(25f, 0.03f, 0.12f, 0.3f),
				LensDistortion = P(-0.3f, 0.04f, 0.04f, 0.16f), Vignette = P(0.1f, 0.04f, 0.1f, 0.3f),
				RadialBlur = P(0.6f, 0.02f, 0.06f, 0.12f), Ripple = P(0.05f, 0.02f, 0f, 0.3f), RippleSpeed = 2f,
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.85f,
			},
		};
	}
}
