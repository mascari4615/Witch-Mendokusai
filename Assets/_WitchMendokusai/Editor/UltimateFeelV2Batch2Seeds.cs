using UnityEngine;
using Pulse = WitchMendokusai.UltimateScreenFeel.Pulse;
using Beat = WitchMendokusai.UltimateScreenFeel.Beat;
using Swell = WitchMendokusai.UltimateScreenFeel.Swell;
using Kind = WitchMendokusai.UltimateScreenFeel.ImpactFrameKind;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 판 V2 배치 2 넷 (번개, 화염, 태양 광선, 블랙홀) 의 화면 연출 박자표 처음 값. 생성기가 에셋이 없을 때 (또는 다시 넣기 메뉴) 한 번 넣음. 그 뒤 정본은 에셋
	/// - 연출 시간은 body (Ult3D_&lt;이름&gt;Heavy) 시간선 기준. 정지 한 박은 닿기 0.005초 전
	/// - 1차 넷 (하늘 검 한 방, 창 왕관 쌓임, 카드 대조, 달 장엄) 과 다른 박자
	///   번개: 첫 타가 가장 크고 그 뒤 연타가 빨라지며 작아짐, 바닥 고리로 마무리
	///   화염: 모음 정지 뒤 1.5초 지속 압력 (떨림이 깔린 채 유지), 끊길 때 작은 마무리
	///   태양 광선: 가장 긴 충전, 발사는 흰 섬광만 (임팩트 프레임 없음), 쓸기 동안 흔들지 않음, 진짜 한 방은 끝 폭발 (늦은 임팩트)
	///   블랙홀: 착지 뒤 빨려드는 고조 (가장 큰 줌과 느려짐), 가장 긴 정지 뒤 폭발은 바깥으로 밀어냄 (줌 되밀림, 렌즈 볼록)
	/// - 섬광 세기 상한은 1차 넷의 0.7배 판과 같은 범위. 번개 연타는 광과민 기준으로 노출 0.2 이하
	/// - 설계와 근거 memo wm/design/ult3d-batch2-20261121, ult-feel-20261120 (흔들림 0.73~3.19% 폭, 펀치 줌 2.2~6.1%, 정지 50~280ms)
	/// </summary>
	public static class UltimateFeelV2Batch2Seeds
	{
		private static Pulse P(float amount, float attack, float hold, float halfLife) => new(amount, attack, hold, halfLife);

		private static AnimationCurve LateRelease() => new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.6f, 0.25f, 1f, 1f), new Keyframe(1f, 1f, 3f, 0f));

		private static AnimationCurve EarlyRelease() => new(new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0.2f, 0f));

		private static AnimationCurve RiseLate() => new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.4f, 0f));

		private static AnimationCurve RiseSteady() => new(new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(1f, 1f, 1.4f, 0f));

		/// <summary> 빨리 올라 평평하게 유지 (지속 압력) </summary>
		private static AnimationCurve RiseFlat() => new(new Keyframe(0f, 0f, 6f, 6f), new Keyframe(0.15f, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

		private static Beat MiniBeat(string label, float fxTime, float trauma, float zoom, float exposure, float radial) => new()
		{
			Label = label, FxTime = fxTime, SlowScale = 1f,
			Trauma = trauma, Zoom = P(zoom, 0.03f, 0.02f, 0.1f), Exposure = P(exposure, 0.015f, 0.02f, 0.08f),
			Bloom = P(exposure * 2f, 0.02f, 0.02f, 0.15f), RadialBlur = P(radial, 0.015f, 0.02f, 0.07f),
		};

		// ---- 번개: 먹구름 고조, 첫 낙뢰 최대, 연쇄 여섯은 빨라지며 작아짐, 바닥 고리 굴절 ----

		public static Swell[] ThunderSwells() => new[]
		{
			new Swell { Label = "먹구름", FxStart = 0.05f, FxEnd = 0.995f, ReleaseSeconds = 0.12f, Shape = RiseSteady(), TimeScale = 0.85f, Exposure = -0.9f, Saturation = -25f, Contrast = 15f, Vignette = 0.3f, Zoom = 0.05f, Dolly = 0.02f, Rumble = 0.18f },
		};

		public static Beat[] ThunderBeats() => new[]
		{
			new Beat
			{
				Label = "첫 낙뢰", FxTime = 0.995f, HoldSeconds = 0.12f, HoldTimeScale = 0f,
				SlowScale = 0.4f, SlowSeconds = 0.4f, SlowRecovery = LateRelease(),
				Trauma = 0.9f, Zoom = P(0.07f, 0.04f, 0.06f, 0.2f), Dolly = P(0.04f, 0.04f, 0.05f, 0.2f),
				Exposure = P(0.9f, 0.015f, 0.03f, 0.12f), Bloom = P(1.8f, 0.02f, 0.08f, 0.3f), Saturation = P(25f, 0.04f, 0.15f, 0.3f), Contrast = P(25f, 0.03f, 0.1f, 0.3f),
				LensDistortion = P(-0.25f, 0.03f, 0.04f, 0.15f), Vignette = P(0.1f, 0.04f, 0.1f, 0.3f),
				RadialBlur = P(0.6f, 0.02f, 0.05f, 0.12f), Ripple = P(0.03f, 0.02f, 0f, 0.25f), RippleSpeed = 1.8f,
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.042f, ImpactFrameStrength = 0.85f,
			},
			MiniBeat("연쇄 1", 1.24f, 0.34f, 0.018f, 0.2f, 0.2f),
			MiniBeat("연쇄 2", 1.4f, 0.3f, 0.016f, 0.18f, 0.16f),
			MiniBeat("연쇄 3", 1.53f, 0.27f, 0.014f, 0.16f, 0.12f),
			MiniBeat("연쇄 4", 1.63f, 0.24f, 0.012f, 0.14f, 0.1f),
			MiniBeat("연쇄 5", 1.71f, 0.22f, 0.01f, 0.12f, 0.08f),
			MiniBeat("연쇄 6", 1.78f, 0.2f, 0.01f, 0.1f, 0.06f),
			new Beat
			{
				Label = "바닥 고리", FxTime = 1.96f, SlowScale = 0.6f, SlowSeconds = 0.35f, SlowRecovery = EarlyRelease(),
				Trauma = 0.5f, Zoom = P(0.03f, 0.03f, 0.04f, 0.15f), Exposure = P(0.35f, 0.02f, 0.03f, 0.12f), Bloom = P(0.9f, 0.03f, 0.05f, 0.25f),
				LensDistortion = P(-0.2f, 0.03f, 0.04f, 0.15f), RadialBlur = P(0.3f, 0.02f, 0.03f, 0.1f),
				Ripple = P(0.05f, 0.02f, 0f, 0.35f), RippleSpeed = 2.2f,
			},
		};

		// ---- 화염: 역류 모음 (당김, 어두움, 느려짐) -> 방출 정지와 되밀림 -> 지속 압력 (깔린 떨림) -> 불기둥 넷 -> 끊김 ----

		public static Swell[] FlameSwells() => new[]
		{
			new Swell { Label = "역류 모음", FxStart = 0f, FxEnd = 0.895f, ReleaseSeconds = 0.08f, Shape = RiseLate(), TimeScale = 0.7f, Exposure = -0.6f, Saturation = -10f, Contrast = 15f, Vignette = 0.3f, Zoom = 0.1f, Dolly = 0.04f, Rumble = 0.25f },
			new Swell { Label = "불길 압력", FxStart = 0.95f, FxEnd = 2.45f, ReleaseSeconds = 0.4f, Shape = RiseFlat(), TimeScale = 1f, Exposure = 0.15f, Saturation = 10f, Contrast = 10f, Vignette = 0.15f, Zoom = 0.03f, Dolly = 0f, Rumble = 0.35f },
		};

		public static Beat[] FlameBeats() => new[]
		{
			new Beat
			{
				Label = "방출", FxTime = 0.895f, HoldSeconds = 0.08f, HoldTimeScale = 0f,
				SlowScale = 0.5f, SlowSeconds = 0.5f, SlowRecovery = LateRelease(),
				Trauma = 0.75f, Zoom = P(-0.05f, 0.04f, 0.05f, 0.2f), Dolly = P(-0.03f, 0.04f, 0.05f, 0.2f),
				Exposure = P(0.7f, 0.02f, 0.05f, 0.14f), Bloom = P(1.6f, 0.03f, 0.12f, 0.35f), Saturation = P(30f, 0.04f, 0.3f, 0.4f), Contrast = P(15f, 0.03f, 0.1f, 0.3f),
				LensDistortion = P(0.15f, 0.03f, 0.05f, 0.15f), Vignette = P(0.08f, 0.04f, 0.1f, 0.3f),
				RadialBlur = P(0.5f, 0.02f, 0.06f, 0.14f),
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.042f, ImpactFrameStrength = 0.7f,
			},
			MiniBeat("불기둥 1", 1.2f, 0.28f, 0.012f, 0.18f, 0.12f),
			MiniBeat("불기둥 2", 1.42f, 0.28f, 0.012f, 0.18f, 0.12f),
			MiniBeat("불기둥 3", 1.64f, 0.3f, 0.014f, 0.2f, 0.14f),
			MiniBeat("불기둥 4", 1.86f, 0.32f, 0.016f, 0.22f, 0.16f),
			new Beat
			{
				Label = "끊김과 연기 버섯", FxTime = 2.3f, SlowScale = 0.55f, SlowSeconds = 0.5f, SlowRecovery = EarlyRelease(),
				Trauma = 0.5f, Zoom = P(0.03f, 0.03f, 0.05f, 0.18f), Exposure = P(0.35f, 0.02f, 0.04f, 0.14f), Bloom = P(0.9f, 0.03f, 0.06f, 0.3f),
				RadialBlur = P(0.3f, 0.02f, 0.04f, 0.1f), Ripple = P(0.03f, 0.02f, 0f, 0.3f), RippleSpeed = 1.2f,
			},
		};

		// ---- 태양 광선: 가장 긴 충전 (어둡게 당김) -> 발사 흰 섬광 (임팩트 프레임 없음) -> 쓸기 (흔들림 없음, 밝음 유지) -> 고리 깨짐 다섯 -> 끝 폭발 최대 ----

		public static Swell[] SunBeamSwells() => new[]
		{
			new Swell { Label = "충전과 물들임", FxStart = 0f, FxEnd = 2.195f, ReleaseSeconds = 0.1f, Shape = RiseLate(), TimeScale = 0.85f, Exposure = -0.8f, Saturation = -20f, Contrast = 10f, Vignette = 0.35f, Zoom = 0.08f, Dolly = 0.03f, Rumble = 0.2f },
			new Swell { Label = "쓸기", FxStart = 2.3f, FxEnd = 4.995f, ReleaseSeconds = 0.1f, Shape = RiseSteady(), TimeScale = 1f, Exposure = 0.1f, Saturation = 15f, Contrast = 12f, Vignette = 0.2f, Zoom = 0.04f, Dolly = 0.01f, Rumble = 0f },
		};

		public static Beat[] SunBeamBeats() => new[]
		{
			new Beat
			{
				Label = "발사", FxTime = 2.195f, HoldSeconds = 0.14f, HoldTimeScale = 0f,
				SlowScale = 0.5f, SlowSeconds = 0.4f, SlowRecovery = LateRelease(),
				Trauma = 0.7f, Zoom = P(0.06f, 0.04f, 0.08f, 0.2f),
				Exposure = P(1f, 0.02f, 0.1f, 0.15f), Bloom = P(2f, 0.02f, 0.12f, 0.3f), Saturation = P(30f, 0.04f, 0.2f, 0.4f), Chromatic = P(0.2f, 0.02f, 0.05f, 0.12f),
				LensDistortion = P(-0.25f, 0.03f, 0.05f, 0.15f), RadialBlur = P(0.65f, 0.02f, 0.06f, 0.14f),
			},
			MiniBeat("고리 1", 2.536f, 0.2f, 0.012f, 0.15f, 0.1f),
			MiniBeat("고리 2", 3.068f, 0.2f, 0.012f, 0.15f, 0.1f),
			MiniBeat("고리 3", 3.6f, 0.2f, 0.012f, 0.15f, 0.1f),
			MiniBeat("고리 4", 4.132f, 0.22f, 0.014f, 0.17f, 0.12f),
			MiniBeat("고리 5", 4.664f, 0.24f, 0.016f, 0.19f, 0.14f),
			new Beat
			{
				Label = "끝 폭발", FxTime = 4.995f, HoldSeconds = 0.1f, HoldTimeScale = 0f,
				SlowScale = 0.35f, SlowSeconds = 0.7f, SlowRecovery = LateRelease(),
				Trauma = 1f, Zoom = P(0.1f, 0.04f, 0.08f, 0.22f), Dolly = P(0.05f, 0.04f, 0.06f, 0.22f),
				Exposure = P(0.85f, 0.02f, 0.05f, 0.14f), Bloom = P(2f, 0.03f, 0.12f, 0.35f), Saturation = P(40f, 0.04f, 0.25f, 0.4f), Contrast = P(25f, 0.03f, 0.12f, 0.3f),
				LensDistortion = P(-0.35f, 0.04f, 0.04f, 0.16f), Vignette = P(0.12f, 0.05f, 0.1f, 0.3f),
				RadialBlur = P(0.85f, 0.02f, 0.06f, 0.12f), Ripple = P(0.04f, 0.02f, 0f, 0.28f), RippleSpeed = 1.6f,
				ImpactFrame = Kind.Mono, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.85f,
			},
		};

		// ---- 블랙홀: 마법진 (조용히 차가워짐) -> 착지 -> 흡입 (가장 큰 당김과 느려짐) -> 가장 긴 정지 -> 폭발은 바깥으로 (줌 되밀림, 렌즈 볼록, 실루엣) ----

		public static Swell[] BlackHoleSwells() => new[]
		{
			new Swell { Label = "마법진", FxStart = 0.1f, FxEnd = 1.3f, ReleaseSeconds = 0.3f, Shape = RiseSteady(), TimeScale = 1f, Exposure = -0.4f, Saturation = -15f, Contrast = 8f, Vignette = 0.2f, Zoom = 0.03f, Dolly = 0f, Rumble = 0f },
			new Swell { Label = "흡입", FxStart = 1.9f, FxEnd = 3.455f, ReleaseSeconds = 0.08f, Shape = RiseSteady(), TimeScale = 0.55f, Exposure = -1f, Saturation = -45f, Contrast = 20f, Vignette = 0.5f, Zoom = 0.14f, Dolly = 0.05f, Rumble = 0.3f },
		};

		public static Beat[] BlackHoleBeats() => new[]
		{
			new Beat
			{
				Label = "착지", FxTime = 1.9f, SlowScale = 0.7f, SlowSeconds = 0.25f, SlowRecovery = EarlyRelease(),
				Trauma = 0.45f, Zoom = P(0.03f, 0.03f, 0.04f, 0.15f), Exposure = P(0.25f, 0.02f, 0.03f, 0.12f), Bloom = P(0.6f, 0.03f, 0.05f, 0.25f),
				LensDistortion = P(-0.15f, 0.03f, 0.04f, 0.15f), Ripple = P(0.03f, 0.02f, 0f, 0.3f), RippleSpeed = 1.4f,
			},
			new Beat
			{
				Label = "붕괴와 폭발", FxTime = 3.455f, HoldSeconds = 0.25f, HoldTimeScale = 0f,
				SlowScale = 0.3f, SlowSeconds = 0.9f, SlowRecovery = LateRelease(),
				Trauma = 1f, Zoom = P(-0.07f, 0.03f, 0.05f, 0.25f), Dolly = P(-0.04f, 0.03f, 0.05f, 0.25f),
				Exposure = P(0.85f, 0.02f, 0.05f, 0.14f), Bloom = P(2f, 0.03f, 0.12f, 0.35f), Saturation = P(55f, 0.04f, 0.3f, 0.45f), Contrast = P(20f, 0.03f, 0.12f, 0.3f),
				LensDistortion = P(0.3f, 0.03f, 0.05f, 0.18f), Vignette = P(0.1f, 0.04f, 0.1f, 0.3f),
				RadialBlur = P(0.9f, 0.02f, 0.06f, 0.14f), Ripple = P(0.05f, 0.02f, 0f, 0.35f), RippleSpeed = 2f,
				ImpactFrame = Kind.Silhouette, ImpactFrameSeconds = 0.084f, ImpactFrameStrength = 0.8f,
			},
		};
	}
}
