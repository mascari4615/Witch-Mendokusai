using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 화면 연출 박자표 (값 정본). 연출 하나에 에셋 하나, UltimateScreenFeelDriver 가 읽어 구동
	/// - 시각 기준 둘: 고조 (Swell) 와 타격 (Beat) 의 시작은 연출 시간 (주제 메시 시간선, 정지 동안 멈춤), 타격 뒤 곡선 (섬광, 흔들림, 느려짐 복귀) 은 실제 시간
	/// - 타격 한 번: 정지 한 박 (hold) -> 임팩트 (섬광, 줌, 흔들림, 느려짐, 방사 흐림, 임팩트 프레임 동시) -> 느린 해제
	/// - 고조 구간: 타격 앞에서 화면이 점점 어둡고 조용해짐 (노출, 채도, 가장자리, 시야각, 시간 배율). 끝 시각에 풀림
	/// - 수치 근거: memo wm/design/ult-feel-20261120 (작품별 화면 장치 실측)
	/// </summary>
	[CreateAssetMenu(fileName = "UltFeel_New", menuName = "WM/CombatFx/Ultimate Screen Feel")]
	public sealed class UltimateScreenFeel : ScriptableObject
	{
		/// <summary> 한 번 솟았다 줄어드는 값. 실제 시간 </summary>
		[Serializable]
		public struct Pulse
		{
			public float Amount;
			[Min(0f), Tooltip("최대까지 오르는 초")] public float Attack;
			[Min(0f), Tooltip("최대에 머무는 초")] public float Hold;
			[Min(0.001f), Tooltip("반으로 줄어드는 초")] public float HalfLife;

			public Pulse(float amount, float attack, float hold, float halfLife)
			{
				Amount = amount;
				Attack = attack;
				Hold = hold;
				HalfLife = halfLife;
			}

			public float Evaluate(float age)
			{
				if (age < 0f || Amount == 0f)
					return 0f;
				if (age < Attack)
					return Amount * UltimateFxRun.EaseOut(age / Attack);
				if (age < Attack + Hold)
					return Amount;
				return Amount * Mathf.Pow(0.5f, (age - Attack - Hold) / Mathf.Max(0.001f, HalfLife));
			}

			/// <summary> 사실상 0 이 되는 나이 (최대의 2%) </summary>
			public float Length => Amount == 0f ? 0f : Attack + Hold + HalfLife * 5.6f;
		}

		public enum ImpactFrameKind
		{
			None,
			/// <summary> 흑백, 대비 올림 </summary>
			Mono,
			/// <summary> 색 반전 </summary>
			Invert,
			/// <summary> 어두운 바탕에 밝은 곳만 흰 실루엣 </summary>
			Silhouette,
		}

		[Serializable]
		public struct Swell
		{
			public string Label;
			[Tooltip("연출 시간. 여기서 오르기 시작")] public float FxStart;
			[Tooltip("연출 시간. 여기서 최대, 곧바로 풀림 (보통 다음 타격 시각)")] public float FxEnd;
			[Min(0.01f), Tooltip("끝 뒤 풀리는 실제 초")] public float ReleaseSeconds;
			[Tooltip("가로 0~1 진행, 세로 0~1 세기")] public AnimationCurve Shape;
			[Range(0.05f, 1f), Tooltip("최대일 때 시간 배율 (주제 메시와 게임 시간 둘 다)")] public float TimeScale;
			public float Exposure;
			public float Saturation;
			public float Contrast;
			[Range(0f, 1f)] public float Vignette;
			[Tooltip("줌 (화면 배율 증가 비율, 0.05 면 5% 크게 보임). 시야각을 좁혀 만듦")] public float Zoom;
			[Tooltip("카메라 전진 (화면 거리 비율). 양수면 다가감")] public float Dolly;
			[Range(0f, 1f), Tooltip("고조 동안 깔리는 미세 떨림 (트라우마 하한)")] public float Rumble;
		}

		[Serializable]
		public struct Beat
		{
			public string Label;
			[Tooltip("연출 시간. 주제 메시가 닿는 순간")] public float FxTime;

			[Header("정지 한 박")]
			[Min(0f), Tooltip("닿기 직전 멈추는 실제 초 (0 이면 없음)")] public float HoldSeconds;
			[Range(0f, 1f), Tooltip("정지 동안 시간 배율. 0 이면 완전 정지")] public float HoldTimeScale;

			[Header("느려짐")]
			[Range(0.05f, 1f), Tooltip("임팩트 직후 시간 배율")] public float SlowScale;
			[Min(0f), Tooltip("배율 1 로 돌아오는 실제 초")] public float SlowSeconds;
			[Tooltip("가로 0~1 진행, 세로 0 (느림) ~ 1 (원래)")] public AnimationCurve SlowRecovery;

			[Header("카메라")]
			[Range(0f, 1f), Tooltip("흔들림 트라우마 추가량")] public float Trauma;
			[Tooltip("펀치 줌 (화면 배율 증가 비율). 실측 쿼터뷰 2~6% 를 67~100ms 에")] public Pulse Zoom;
			[Tooltip("카메라 전진 (화면 거리 비율)")] public Pulse Dolly;

			[Header("후처리")]
			[Tooltip("노출 (EV)")] public Pulse Exposure;
			public Pulse Bloom;
			[Tooltip("채도 (-100~100 단위 더함)")] public Pulse Saturation;
			[Tooltip("대비 (-100~100 단위 더함)")] public Pulse Contrast;
			public Pulse Chromatic;
			[Tooltip("렌즈 왜곡 (음수면 가운데가 오목, 충격에 빨려드는 느낌)")] public Pulse LensDistortion;
			public Pulse Vignette;

			[Header("화면 왜곡 (렌더러 기능)")]
			[Tooltip("표적 중심 방사 흐림 세기")] public Pulse RadialBlur;
			[Tooltip("표적 중심 굴절 고리 세기")] public Pulse Ripple;
			[Min(0f), Tooltip("고리 반지름이 커지는 속도 (화면 높이/초)")] public float RippleSpeed;

			[Header("임팩트 프레임")]
			public ImpactFrameKind ImpactFrame;
			[Min(0f), Tooltip("임팩트 프레임 길이 (실제 초). 24fps 기준 1장 0.042")] public float ImpactFrameSeconds;
			[Range(0f, 1f)] public float ImpactFrameStrength;
		}

		[Serializable]
		public struct TraumaSettings
		{
			[Range(0f, 0.1f), Tooltip("트라우마 1 일 때 최대 이동 (화면 폭 비율)")] public float MaxOffset;
			[Range(0f, 10f), Tooltip("트라우마 1 일 때 최대 기울기 (도)")] public float MaxRoll;
			[Min(0.1f), Tooltip("떨림 빠르기 (Hz)")] public float Frequency;
			[Min(0.01f), Tooltip("초당 줄어드는 트라우마")] public float DecayPerSecond;
			[Range(0f, 1f), Tooltip("세로 비중. 1 이면 세로로만")] public float VerticalBias;
			[Tooltip("트라우마 -> 흔들림 세기. 보통 제곱")] public AnimationCurve Response;
		}

		[SerializeField, Tooltip("광과민 대비 세기 줄이기. 비우면 그대로")] private UltimateFeelComfort comfort;
		[SerializeField] private TraumaSettings trauma = new()
		{
			MaxOffset = 0.025f,
			MaxRoll = 1.5f,
			Frequency = 22f,
			DecayPerSecond = 2.2f,
			VerticalBias = 0.7f,
			Response = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f)),
		};
		[SerializeField] private Swell[] swells = Array.Empty<Swell>();
		[SerializeField] private Beat[] beats = Array.Empty<Beat>();
		[SerializeField, Min(0f), Tooltip("방사 흐림 표본 거리 상한 (화면 비율)")] private float radialBlurReach = 0.12f;
		[SerializeField, Min(0.001f), Tooltip("굴절 고리 띠 폭 (화면 높이 비율)")] private float rippleWidth = 0.06f;
		[SerializeField, Range(0f, 0.5f), Tooltip("렌즈 왜곡 세기 1 당 확대 보정 (가장자리 빈 곳 방지)")] private float lensScaleCompensation = 0.18f;
		[SerializeField, Tooltip("표적의 화면 위쪽 거리 (시전자 기준). 화면 왜곡 중심과 카메라 기준점")] private float targetForward = 2.8f;

		public UltimateFeelComfort Comfort => comfort;
		public TraumaSettings Trauma => trauma;
		public Swell[] Swells => swells;
		public Beat[] Beats => beats;
		public float RadialBlurReach => radialBlurReach;
		public float RippleWidth => rippleWidth;
		public float LensScaleCompensation => lensScaleCompensation;
		public float TargetForward => targetForward;

#if UNITY_EDITOR
		/// <summary> 프리셋 생성기 (UltimateFeelPresetBuilder) 가 처음 값을 한 번 넣을 때만. 그 뒤 정본은 인스펙터 </summary>
		public void EditorSeed(UltimateFeelComfort comfortAsset, TraumaSettings traumaSettings, Swell[] swellList, Beat[] beatList, float targetForwardDistance)
		{
			comfort = comfortAsset;
			trauma = traumaSettings;
			swells = swellList;
			beats = beatList;
			targetForward = targetForwardDistance;
		}
#endif

		/// <summary> 정지 한 박 합 (실제 시간이 연출 시간보다 늘어나는 양의 하한) </summary>
		public float TotalHoldSeconds
		{
			get
			{
				float total = 0f;
				foreach (Beat beat in beats)
					total += beat.HoldSeconds;
				return total;
			}
		}
	}
}
