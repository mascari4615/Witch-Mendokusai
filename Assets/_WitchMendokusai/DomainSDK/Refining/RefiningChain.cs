using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.DomainSDK.Refining
{
	// 정련 체인의 결정적 누적 모델 — 비전-중립 순수 함수(스킨 무관). 계수 = RefiningCoefficients 주입.
	// clamp 범위는 타입 출력 계약(튜닝 X) — RciDemandModel DEMAND_MIN/MAX 선례.
	public static class RefiningChain
	{
		private const float QUALITY_MIN = 0f;
		private const float QUALITY_MAX = 1f;
		private const float WARMTH_MIN = -1f;
		private const float WARMTH_MAX = 1f;

		public static RefiningState Initial(RefiningCoefficients coefficients)
		{
			float quality = Mathf.Clamp(coefficients.InitialQuality, QUALITY_MIN, QUALITY_MAX);
			return new RefiningState(quality, 0f, 0);
		}

		public static RefiningState ApplyStage(RefiningState current, RefiningStage stage, RefiningCoefficients coefficients)
		{
			float qualityDelta = QualityDelta(stage.Approach, coefficients);
			float warmthDelta = WarmthDelta(stage.Approach, coefficients);

			float nextQuality = Mathf.Clamp(current.Quality + qualityDelta, QUALITY_MIN, QUALITY_MAX);
			float nextWarmth = Mathf.Clamp(current.Warmth + warmthDelta, WARMTH_MIN, WARMTH_MAX);

			return new RefiningState(nextQuality, nextWarmth, current.CompletedStages + 1);
		}

		public static RefiningState Evaluate(IReadOnlyList<RefiningStage> stages, RefiningCoefficients coefficients)
		{
			RefiningState state = Initial(coefficients);
			for (int i = 0; i < stages.Count; i++)
			{
				state = ApplyStage(state, stages[i], coefficients);
			}
			return state;
		}

		private static float QualityDelta(RefiningApproach approach, RefiningCoefficients coefficients) => approach switch
		{
			RefiningApproach.Fast => coefficients.FastQualityDelta,
			RefiningApproach.Careful => coefficients.CarefulQualityDelta,
			_ => 0f,
		};

		private static float WarmthDelta(RefiningApproach approach, RefiningCoefficients coefficients) => approach switch
		{
			RefiningApproach.Fast => coefficients.FastWarmthDelta,
			RefiningApproach.Careful => coefficients.CarefulWarmthDelta,
			_ => 0f,
		};
	}
}
