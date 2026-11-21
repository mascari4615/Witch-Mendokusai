using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 트라우마 흔들림 계산. 궁극기 화면 연출 구동기와 플레이어 피격 연출이 같이 씀
	/// - 타격마다 트라우마를 더하고 초당 일정량 줄임. 흔들림 세기는 트라우마의 곡선 (보통 제곱) 이라 끝이 빨리 잦아듦
	/// - 이동과 기울기는 펄린 잡음. 같은 씨앗이면 녹화마다 같은 흔들림
	/// - 카메라에 얹는 일은 UltimateFeelCamera 가 맡음. 여기는 값만 계산
	/// </summary>
	public sealed class TraumaShake
	{
		private readonly float noiseSeed;
		private float trauma;

		public TraumaShake(float noiseSeed)
		{
			this.noiseSeed = noiseSeed;
		}

		public float Trauma => trauma;

		/// <summary> 트라우마를 더함. 상한 위로는 안 올리고, 이미 상한보다 크면 그대로 </summary>
		public void Add(float amount, float cap = 1f)
		{
			if (amount <= 0f)
				return;
			trauma = Mathf.Max(trauma, Mathf.Min(Mathf.Clamp01(cap), trauma + amount));
		}

		public void Clear() => trauma = 0f;

		/// <param name="floor"> 이 아래로는 줄지 않음 (궁극기 고조 구간의 지속 떨림) </param>
		/// <param name="strength"> 세기 배율 (세기 줄이기 설정) </param>
		/// <param name="offset"> 화면 폭 비율 이동 </param>
		/// <param name="roll"> 기울기 (도) </param>
		public void Step(in UltimateScreenFeel.TraumaSettings settings, float realNow, float realDelta, float floor, float strength, out Vector2 offset, out float roll)
		{
			trauma = Mathf.Max(floor, trauma - settings.DecayPerSecond * realDelta);
			float shake = settings.Response != null && settings.Response.length > 0 ? Mathf.Clamp01(settings.Response.Evaluate(trauma)) : trauma * trauma;
			shake *= strength;
			float time = realNow * settings.Frequency;
			float horizontal = Mathf.Lerp(1f, 0.3f, settings.VerticalBias);
			float vertical = Mathf.Lerp(0.3f, 1f, settings.VerticalBias);
			offset = new Vector2(
				(Mathf.PerlinNoise(noiseSeed, time) * 2f - 1f) * horizontal * settings.MaxOffset * shake,
				(Mathf.PerlinNoise(noiseSeed + 17.3f, time) * 2f - 1f) * vertical * settings.MaxOffset * shake);
			roll = (Mathf.PerlinNoise(noiseSeed + 41.7f, time) * 2f - 1f) * settings.MaxRoll * shake;
		}
	}
}
