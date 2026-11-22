using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// BillboardShader 의 젤 출렁임을 C# 에서 다시 계산. 사망 잔상이 죽는 순간의 늘어난 모양에서 시작하게 함.
	/// 셰이더 정점 단계와 같은 식이어야 함 (Billboard.shader 의 _JellyWobble 블록)
	/// </summary>
	public static class JellyWobbleMath
	{
		private static readonly int JELLY_WOBBLE_ID = Shader.PropertyToID("_JellyWobble");
		private static readonly int JELLY_VOLUME_ID = Shader.PropertyToID("_JellyVolume");
		private const float TAU = Mathf.PI * 2f;

		/// <summary> 지금 시각, 피벗 위치에서의 (가로, 세로) 배율. 출렁임이 없는 재질이면 (1, 1) </summary>
		public static Vector2 ScaleFactor(Material material, Vector3 pivotPosition)
		{
			if (material == null || material.HasProperty(JELLY_WOBBLE_ID) == false)
				return Vector2.one;
			Vector4 wobbleSettings = material.GetVector(JELLY_WOBBLE_ID);
			if (wobbleSettings.x <= 0f)
				return Vector2.one;
			float volume = material.HasProperty(JELLY_VOLUME_ID) ? material.GetFloat(JELLY_VOLUME_ID) : 0f;
			// 셰이더의 _Time.y 는 장면을 불러온 뒤 흐른 게임 시간
			float phase = pivotPosition.x * wobbleSettings.z + pivotPosition.z * wobbleSettings.w;
			float wobble = wobbleSettings.x * Mathf.Sin(Time.timeSinceLevelLoad * wobbleSettings.y * TAU + phase);
			return new Vector2(1f - wobble * volume, 1f + wobble);
		}
	}
}
