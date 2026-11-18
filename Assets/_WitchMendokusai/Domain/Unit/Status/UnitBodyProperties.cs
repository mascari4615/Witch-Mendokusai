using UnityEngine;

namespace WitchMendokusai
{
	// 유닛 그림 재질 속성 블록을 같이 쓰는 자리. 피격 점멸, 피격 흔들림, 상태이상 물들임
	// - 쓰는 쪽마다 블록을 받아서 자기 값만 덧씀
	// - 셋 다 꺼지면 블록 해제. 재질 배치 유지
	public static class UnitBodyProperties
	{
		public static readonly int EMISSION_ID = Shader.PropertyToID("_Emission");
		public static readonly int STATUS_TINT_ID = Shader.PropertyToID("_StatusTint");
		public static readonly int STATUS_RIM_ID = Shader.PropertyToID("_StatusRim");
		public static readonly int STATUS_RIM_WIDTH_ID = Shader.PropertyToID("_StatusRimWidth");
		// x 시작 시각 (셰이더 _Time.y 기준), y 폭, z 시간, w 초당 왕복
		public static readonly int HIT_SHAKE_ID = Shader.PropertyToID("_HitShake");

		// 셰이더 _Time.y 와 같은 시계
		public static float ShaderTime => Time.timeSinceLevelLoad;

		public static bool IsShaking(MaterialPropertyBlock block)
		{
			Vector4 shake = block.GetVector(HIT_SHAKE_ID);
			return shake.y > 0f && ShaderTime - shake.x < shake.z;
		}

		// 지금 남은 흔들림 폭. 감쇠 곡선은 셰이더와 같음
		public static float RemainingShake(MaterialPropertyBlock block)
		{
			Vector4 shake = block.GetVector(HIT_SHAKE_ID);
			if (shake.y <= 0f || shake.z <= 0f)
				return 0f;
			float progress = (ShaderTime - shake.x) / shake.z;
			if (progress < 0f || progress >= 1f)
				return 0f;
			float decay = 1f - progress;
			return shake.y * decay * decay;
		}

		public static void Commit(SpriteRenderer spriteRenderer, MaterialPropertyBlock block)
		{
			bool idle = block.GetFloat(EMISSION_ID) <= 0f
				&& block.GetColor(STATUS_TINT_ID).a <= 0f
				&& block.GetColor(STATUS_RIM_ID).a <= 0f
				&& IsShaking(block) == false;
			spriteRenderer.SetPropertyBlock(idle ? null : block);
		}
	}
}
