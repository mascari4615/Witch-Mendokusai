using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 플레이어 피격 화면 연출 수치. SurvivalFeelConfig 안에 둠
	/// - 세기 0~1 은 받은 피해 / 최대 체력에서. FullStrengthDamageRatio 이상이면 1. 각 장치는 세기 0 (작은 피해) 값과 1 값 사이를 보간
	/// - 흔들림은 궁극기와 같은 트라우마 방식 (TraumaShake), 정지는 TimeManager.HitStop, 가장자리 빨강은 저체력 경고와 같은 화면 이미지 한 장
	/// </summary>
	[Serializable]
	public class PlayerHitFeelSettings
	{
		[Header("세기")]
		[Range(0.01f, 1f), Tooltip("이 피해 비율 (피해 / 최대 체력) 이상이면 세기 1")] public float FullStrengthDamageRatio = 0.25f;
		[Min(0f), Tooltip("앞 피격 뒤 이 초 안의 피격은 정지와 가장자리 번쩍임을 새로 시작하지 않고 흔들림만 상한까지 더함. 평소에는 무적 시간 (0.5초) 이 먼저 거름. 무적 시간을 이보다 짧게 줄일 때 대비. 0.34 이상이면 가장자리 재점등이 초당 3회 미만 (광과민 기준)")] public float MinInterval = 0.35f;

		[Header("화면 흔들림")]
		[Range(0f, 1f), Tooltip("세기 0 일 때 더하는 트라우마")] public float TraumaAtMin = 0.45f;
		[Range(0f, 1f), Tooltip("세기 1 일 때 더하는 트라우마")] public float TraumaAtMax = 0.85f;
		[Range(0f, 1f), Tooltip("쌓인 트라우마 상한. 연속 피격에서 이 위로 안 쌓임")] public float TraumaCap = 0.9f;
		[Tooltip("트라우마 흔들림 모양. 궁극기 박자표와 같은 칸")]
		public UltimateScreenFeel.TraumaSettings Trauma = new()
		{
			MaxOffset = 0.045f,
			MaxRoll = 1.2f,
			Frequency = 15f,
			DecayPerSecond = 2.4f,
			VerticalBias = 0.5f,
			// 궁극기는 제곱, 피격은 직선. 한 번 맞는 짧은 흔들림이라 작은 피격도 보이게
			Response = AnimationCurve.Linear(0f, 0f, 1f, 1f),
		};

		[Header("정지")]
		[Min(0f), Tooltip("세기 0 일 때 정지 초. 24fps 1장은 0.042")] public float HitStopAtMin = 0.04f;
		[Min(0f), Tooltip("세기 1 일 때 정지 초. 3장 이하")] public float HitStopAtMax = 0.1f;
		[Range(0.0001f, 1f), Tooltip("정지 동안 시간 배율")] public float HitStopScale = 0.05f;

		[Header("가장자리 빨강")]
		[Tooltip("피격 가장자리 색. 저체력 경고 색보다 밝고 짙게 (풀밭 위에서 갈색으로 섞이지 않게)")] public Color EdgeColor = new(1f, 0.04f, 0.06f, 1f);
		[Range(0f, 1f), Tooltip("세기 0 일 때 피격 직후 가장자리 불투명도")] public float EdgeAlphaAtMin = 0.55f;
		[Range(0f, 1f), Tooltip("세기 1 일 때 피격 직후 가장자리 불투명도")] public float EdgeAlphaAtMax = 0.9f;
		[Min(0f), Tooltip("가장 짙은 채로 머무는 초")] public float EdgeHoldSeconds = 0.1f;
		[Min(0.01f), Tooltip("짙음에서 사라지기까지 초")] public float EdgeFadeSeconds = 0.6f;
		[Range(0f, 1f), Tooltip("피격 번쩍임용 가장자리 투명 반경 (모서리 기준 비율). 저체력 경고보다 안쪽까지")] public float EdgeInnerRadius = 0.32f;

		[Header("몸 번쩍임")]
		[ColorUsage(false, true), Tooltip("피격 직후 몸 색. 재질 기본색에 곱함 (1 넘으면 밝아짐)")] public Color BodyFlashColor = new(2.6f, 0.75f, 0.75f, 1f);
		[Min(0f), Tooltip("피격 직후 몸 번쩍임 초")] public float BodyFlashSeconds = 0.12f;
		[Range(0f, 1f), Tooltip("무적 시간 깜빡임의 몸 색 세기 (0 이면 무적 깜빡임 없음)")] public float InvincibleBlinkStrength = 0.45f;

		/// <summary> 받은 피해의 세기 0~1 </summary>
		public float StrengthOf(int damage, int maxHealth)
		{
			if (maxHealth <= 0)
				return 1f;
			float ratio = Mathf.Max(0, damage) / (float)maxHealth;
			return Mathf.Clamp01(ratio / Mathf.Max(0.0001f, FullStrengthDamageRatio));
		}
	}
}
