using UnityEngine;

namespace WitchMendokusai
{
	// 오브 한 개의 시각 값 계산. 상태 없음, 개체별 위상만 받음
	public static class ExpOrbMotion
	{
		private const float GOLDEN_STEP = 0.618034f;

		public static float PhaseOf(int spawnIndex) => spawnIndex * GOLDEN_STEP % 1f;

		public static int TierIndex(System.Collections.Generic.IReadOnlyList<ExpGemTier> tiers, int value)
		{
			int index = 0;
			for (int i = 0; i < tiers.Count; i++)
				if (value >= tiers[i].MinValue)
					index = i;
			return index;
		}

		public static float Size(SurvivalFeelConfig config, float tierScale, float phase, float time)
		{
			float pulse = 1f + config.ExpOrbPulseAmplitude * Mathf.Sin((config.ExpOrbPulseSpeed * time + phase) * Mathf.PI * 2f);
			return config.ExpOrbSize * config.ExpOrbCubeRatio * tierScale * pulse;
		}

		public static Quaternion Rotation(SurvivalFeelConfig config, float phase, float time)
		{
			Vector3 tilt = config.ExpOrbTilt;
			return Quaternion.Euler(tilt.x, tilt.y + config.ExpOrbSpinSpeed * time + phase * 360f, tilt.z);
		}

		public static float Lift(SurvivalFeelConfig config, float phase, float time)
		{
			float wave = 0.5f + 0.5f * Mathf.Sin((config.ExpOrbBobSpeed * time + phase) * Mathf.PI * 2f);
			return config.ExpOrbBobAmplitude * wave;
		}
	}
}
