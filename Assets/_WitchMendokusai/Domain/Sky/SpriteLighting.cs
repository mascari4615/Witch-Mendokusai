using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 빌보드 그림 (몬스터, NPC, 전리품) 이 장면 빛을 받는 정도. 하늘 설정 (SkyPresetSO) 이 들고 셰이더 전역값으로 넣음
	/// - 셰이더: WitchMendokusai/BillboardShader 의 SpriteLight. 해 x 비중 + 환경광을 기준값으로 나눠 1에 막음 (낮은 지금과 같음)
	/// - 바닥값: 밤에도 이 밝기 아래로 안 내려감 (정본 "밤도 칙칙하지 않게")
	/// - 추가 광원 (횃불, 궁극기 점광): 위 값에 더함. 물체당 최대 개수로 비용 조절
	/// </summary>
	[Serializable]
	public sealed class SpriteLightingSettings
	{
		[field: SerializeField, Range(0f, 1f), Tooltip("0 이면 빛 무시 (예전 그림 그대로), 1 이면 아래 값 그대로")]
		public float Response { get; private set; } = 1f;

		[field: SerializeField, Min(0.01f), Tooltip("해 x 비중 + 환경광이 이 값이면 밝기 1. 낮 값보다 작게 두면 낮 화면이 예전과 같음")]
		public float Reference { get; private set; } = 1f;

		[field: SerializeField, Min(0f), Tooltip("해 빛을 더하는 비중. 판 그림이라 방향 음영 없이 색과 세기만")]
		public float SunWeight { get; private set; } = .5f;

		[field: SerializeField, Range(0f, 1f), Tooltip("밝기 바닥값 (RGB 가중 밝기). 밤 몬스터가 바닥에 묻히지 않게")]
		public float Floor { get; private set; } = .6f;

		[field: SerializeField, Min(0f), Tooltip("추가 광원 (점광, 스포트) 세기 배율")]
		public float PointGain { get; private set; } = .12f;

		[field: SerializeField, Min(1f), Tooltip("빛 배율 상한. 광원 바로 옆에서 하얗게 타지 않게")]
		public float Cap { get; private set; } = 1.8f;

		[field: SerializeField, Range(0, 4), Tooltip("그림 하나가 받는 추가 광원 수. URP 물체당 상한 4. 0 이면 횃불과 궁극기 광원 무시 (비용 0)")]
		public int MaxAdditionalLights { get; private set; } = 4;

		public static SpriteLightingSettings Default { get; } = new();
	}

	public static class SpriteLighting
	{
		public static readonly int RESPONSE_ID = Shader.PropertyToID("_WMSpriteLightResponse");
		public static readonly int REFERENCE_ID = Shader.PropertyToID("_WMSpriteLightReference");
		public static readonly int SUN_WEIGHT_ID = Shader.PropertyToID("_WMSpriteSunWeight");
		public static readonly int FLOOR_ID = Shader.PropertyToID("_WMSpriteLightFloor");
		public static readonly int POINT_GAIN_ID = Shader.PropertyToID("_WMSpritePointGain");
		public static readonly int CAP_ID = Shader.PropertyToID("_WMSpriteLightCap");
		public static readonly int MAX_COUNT_ID = Shader.PropertyToID("_WMSpriteLightMaxCount");

		public static void Apply(SpriteLightingSettings settings)
		{
			settings ??= SpriteLightingSettings.Default;
			Shader.SetGlobalFloat(RESPONSE_ID, settings.Response);
			Shader.SetGlobalFloat(REFERENCE_ID, settings.Reference);
			Shader.SetGlobalFloat(SUN_WEIGHT_ID, settings.SunWeight);
			Shader.SetGlobalFloat(FLOOR_ID, settings.Floor);
			Shader.SetGlobalFloat(POINT_GAIN_ID, settings.PointGain);
			Shader.SetGlobalFloat(CAP_ID, settings.Cap);
			Shader.SetGlobalFloat(MAX_COUNT_ID, settings.MaxAdditionalLights);
		}

		/// <summary> 연출이 잠깐 추가 광원 배율과 상한을 덮을 때. 하늘 감독은 시각이 바뀔 때만 다시 넣으므로 매 프레임 부름 </summary>
		public static void OverridePoint(float pointGain, float cap)
		{
			Shader.SetGlobalFloat(POINT_GAIN_ID, pointGain);
			Shader.SetGlobalFloat(CAP_ID, cap);
		}
	}
}
