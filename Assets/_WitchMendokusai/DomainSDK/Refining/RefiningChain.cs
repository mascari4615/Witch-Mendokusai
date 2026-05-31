using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.DomainSDK.Refining
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — 정련 체인의 결정적 누적 모델(DomainSDK, 순수 함수, EditMode 직접 테스트).
	/// "잔재 1개를 여러 정련 단계를 거치게 하면 품질 등급이 오르고, 단계 선택(빠름/정성)에 따라 최종 등급·온기가 달라지는"
	/// 1재료 수직 슬라이스의 코어. Phase 1 이상에서 잔재 종류·단계 가중치·일꾼 생성이 이 위에 얹힌다.
	///
	/// 비전-중립 — 마계 사체/시들어버린 잔재/마도서 페이지 등 스킨 무관(순수 수학). 계수 = RefiningCoefficients 주입.
	/// Quality [0,1] / Warmth [-1,1] clamp = 타입 범위 상수(튜닝 수치 X — 범위 자체가 출력 계약).
	/// </summary>
	public static class RefiningChain
	{
		// 출력 계약 — 튜닝 수치 아닌 타입 범위(RciDemandModel DEMAND_MIN/MAX 선례).
		private const float QUALITY_MIN = 0f;
		private const float QUALITY_MAX = 1f;
		private const float WARMTH_MIN = -1f;
		private const float WARMTH_MAX = 1f;

		/// <summary>잔재 원자재 시작 상태 — 정련 한 단계도 거치지 않은 baseline. Warmth 는 중립(0).</summary>
		public static RefiningState Initial(RefiningCoefficients coefficients)
		{
			float quality = Mathf.Clamp(coefficients.InitialQuality, QUALITY_MIN, QUALITY_MAX);
			return new RefiningState(quality, 0f, 0);
		}

		/// <summary>한 단계 진행 — 태도(Fast/Careful)에 따른 품질·온기 델타 적용 + clamp. CompletedStages++.</summary>
		public static RefiningState ApplyStage(RefiningState current, RefiningStage stage, RefiningCoefficients coefficients)
		{
			float qualityDelta = QualityDelta(stage.Approach, coefficients);
			float warmthDelta = WarmthDelta(stage.Approach, coefficients);

			float nextQuality = Mathf.Clamp(current.Quality + qualityDelta, QUALITY_MIN, QUALITY_MAX);
			float nextWarmth = Mathf.Clamp(current.Warmth + warmthDelta, WARMTH_MIN, WARMTH_MAX);

			return new RefiningState(nextQuality, nextWarmth, current.CompletedStages + 1);
		}

		/// <summary>체인 전체 평가 — Initial 에서 시작해 stages 를 순서대로 ApplyStage. stages 비면 Initial 그대로.</summary>
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
