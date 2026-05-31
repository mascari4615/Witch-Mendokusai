using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Refining;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-172 Phase 0 — <see cref="RefiningChain"/> 정련 누적 체인 회귀 잠금.
	///
	/// 스펙 필수 검증:
	///   ① 품질 누적 결정성   (같은 입력 = 같은 출력 — Quality/Warmth/CompletedStages 셋 다)
	///   ② 빠름 vs 정성 분기  (Careful 누적 > Fast 누적 in Quality / Warmth 부호 정반대)
	///   ③ 레거시 단순제작 동등 (1단계 체인 = Initial → ApplyStage 1회 와 동일 — 숨겨진 체인 보너스 0)
	///
	/// 순수 함수 — MonoBehaviour/VContainer/PlayMode 0. new() · static call 직접.
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class RefiningChainTest
	{
		// 테스트 계수 — 명시 주입(POCO 디폴트 없음). 게임에선 RefiningCoefficientsSO 가 공급.
		// 비대칭이 의도: Careful 의 품질 델타(0.2) > Fast(0.05) / Fast 온기(-0.2) vs Careful(+0.2).
		// "효율 vs 윤리"가 단순 손익이 아니라 톤 정합 — 수치 자체가 비대칭이어야 의미 있는 선택.
		private static RefiningCoefficients DefaultCoefficients()
		{
			return new RefiningCoefficients(
				initialQuality: 0.0f,
				fastQualityDelta: 0.05f,
				carefulQualityDelta: 0.2f,
				fastWarmthDelta: -0.2f,
				carefulWarmthDelta: 0.2f);
		}

		[Test]
		public void Initial_StartsAtCoefficientBaseline_WithNeutralWarmth()
		{
			RefiningState state = RefiningChain.Initial(DefaultCoefficients());

			Assert.That(state.Quality, Is.EqualTo(0f), "잔재 원자재 = baseline 품질");
			Assert.That(state.Warmth, Is.EqualTo(0f), "정련 0회 = 온기 중립(아직 손대지 않음)");
			Assert.That(state.CompletedStages, Is.EqualTo(0));
		}

		[Test]
		public void EmptyChain_ReturnsInitialState()
		{
			RefiningState state = RefiningChain.Evaluate(new List<RefiningStage>(), DefaultCoefficients());

			Assert.That(state.Quality, Is.EqualTo(0f), "빈 체인 = Initial 그대로");
			Assert.That(state.Warmth, Is.EqualTo(0f));
			Assert.That(state.CompletedStages, Is.EqualTo(0), "단계 0회");
		}

		// ① 품질 누적 결정성 — 스펙 필수.
		[Test]
		public void Evaluate_IsDeterministic_SameInputSameOutput()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			List<RefiningStage> stages = new()
			{
				new RefiningStage(RefiningStageKind.Dissection, RefiningApproach.Careful),
				new RefiningStage(RefiningStageKind.Purification, RefiningApproach.Fast),
				new RefiningStage(RefiningStageKind.Refinement, RefiningApproach.Careful),
			};

			RefiningState first = RefiningChain.Evaluate(stages, coefficients);
			RefiningState second = RefiningChain.Evaluate(stages, coefficients);

			Assert.That(second.Quality, Is.EqualTo(first.Quality), "결정성 — 품질");
			Assert.That(second.Warmth, Is.EqualTo(first.Warmth), "결정성 — 온기");
			Assert.That(second.CompletedStages, Is.EqualTo(first.CompletedStages));
		}

		// ② 빠름 vs 정성 분기 — 스펙 필수: 같은 단계 수에도 태도가 등급/온기를 가른다.
		[Test]
		public void AllCareful_AchievesHigherQuality_ThanAllFast()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			List<RefiningStage> fastChain = MakeChain(RefiningApproach.Fast, 3);
			List<RefiningStage> carefulChain = MakeChain(RefiningApproach.Careful, 3);

			float fastQuality = RefiningChain.Evaluate(fastChain, coefficients).Quality;
			float carefulQuality = RefiningChain.Evaluate(carefulChain, coefficients).Quality;

			Assert.That(carefulQuality, Is.GreaterThan(fastQuality), "정성 = 등급 더 높이 끌어올림");
		}

		[Test]
		public void AllCareful_AccumulatesWarmth()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			RefiningState state = RefiningChain.Evaluate(MakeChain(RefiningApproach.Careful, 3), coefficients);

			Assert.That(state.Warmth, Is.GreaterThan(0f), "애도하며 정련 = 온기 +");
		}

		[Test]
		public void AllFast_DepletesWarmth()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			RefiningState state = RefiningChain.Evaluate(MakeChain(RefiningApproach.Fast, 3), coefficients);

			Assert.That(state.Warmth, Is.LessThan(0f), "함부로 정련 = 온기 - (마을 식어감)");
		}

		// ③ 레거시 단순제작 동등 — 스펙 필수: 1단계 체인 = ApplyStage 1회. 체인이 숨은 보너스를 박지 않는다.
		[Test]
		public void SingleStageChain_EqualsOneManualApply()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			RefiningStage stage = new(RefiningStageKind.Dissection, RefiningApproach.Careful);

			RefiningState viaChain = RefiningChain.Evaluate(new List<RefiningStage> { stage }, coefficients);
			RefiningState viaManual = RefiningChain.ApplyStage(RefiningChain.Initial(coefficients), stage, coefficients);

			Assert.That(viaChain.Quality, Is.EqualTo(viaManual.Quality), "단순제작(1단계) = 체인 1회 — 숨은 보너스 0");
			Assert.That(viaChain.Warmth, Is.EqualTo(viaManual.Warmth));
			Assert.That(viaChain.CompletedStages, Is.EqualTo(viaManual.CompletedStages));
		}

		[Test]
		public void MixedApproach_AccumulatesIndependently_NoCancel()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			// Fast +0.05, Careful +0.2 → 합 0.25. 서로 상쇄 X, 단순 합산.
			List<RefiningStage> mixed = new()
			{
				new RefiningStage(RefiningStageKind.Dissection, RefiningApproach.Fast),
				new RefiningStage(RefiningStageKind.Purification, RefiningApproach.Careful),
			};

			RefiningState state = RefiningChain.Evaluate(mixed, coefficients);

			Assert.That(state.Quality, Is.EqualTo(0.25f).Within(0.0001f), "Fast(0.05) + Careful(0.2) = 0.25");
			Assert.That(state.Warmth, Is.EqualTo(0f).Within(0.0001f), "Fast(-0.2) + Careful(+0.2) = 0 (상쇄)");
			Assert.That(state.CompletedStages, Is.EqualTo(2));
		}

		[Test]
		public void CompletedStages_IncrementsPerApply()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			RefiningState state = RefiningChain.Initial(coefficients);
			RefiningStage stage = new(RefiningStageKind.Refinement, RefiningApproach.Fast);

			state = RefiningChain.ApplyStage(state, stage, coefficients);
			Assert.That(state.CompletedStages, Is.EqualTo(1));
			state = RefiningChain.ApplyStage(state, stage, coefficients);
			Assert.That(state.CompletedStages, Is.EqualTo(2));
			state = RefiningChain.ApplyStage(state, stage, coefficients);
			Assert.That(state.CompletedStages, Is.EqualTo(3));
		}

		[Test]
		public void Quality_ClampedToValidRange_NoOverflow()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			// 정성 단계(0.2)를 20회 거치면 4.0 이지만, [0,1] clamp 로 정확히 1.0.
			RefiningState state = RefiningChain.Evaluate(MakeChain(RefiningApproach.Careful, 20), coefficients);

			Assert.That(state.Quality, Is.EqualTo(1f), "Quality 는 [0,1] clamp — 최고 등급 초과 X");
		}

		[Test]
		public void Warmth_ClampedToSignedUnitRange()
		{
			RefiningCoefficients coefficients = DefaultCoefficients();
			// Fast(-0.2) 20회 = -4.0 이지만 clamp -1. 반대로 Careful 20회 = +4 → clamp +1.
			RefiningState callous = RefiningChain.Evaluate(MakeChain(RefiningApproach.Fast, 20), coefficients);
			RefiningState mourning = RefiningChain.Evaluate(MakeChain(RefiningApproach.Careful, 20), coefficients);

			Assert.That(callous.Warmth, Is.EqualTo(-1f), "온기 [-1,1] clamp — 함부로 누적 바닥");
			Assert.That(mourning.Warmth, Is.EqualTo(1f), "온기 [-1,1] clamp — 애도 누적 천장");
		}

		[Test]
		public void InitialQuality_RespectsBaseline_ClampedToRange()
		{
			// baseline 0.3 = 이미 한 번 정화된 잔재 가정. Initial 이 반영해야.
			RefiningCoefficients midBaseline = new(initialQuality: 0.3f, fastQualityDelta: 0.05f, carefulQualityDelta: 0.2f, fastWarmthDelta: -0.2f, carefulWarmthDelta: 0.2f);
			Assert.That(RefiningChain.Initial(midBaseline).Quality, Is.EqualTo(0.3f), "baseline 반영");

			// baseline 비정상값(1.5) → [0,1] clamp.
			RefiningCoefficients overshoot = new(initialQuality: 1.5f, fastQualityDelta: 0.05f, carefulQualityDelta: 0.2f, fastWarmthDelta: -0.2f, carefulWarmthDelta: 0.2f);
			Assert.That(RefiningChain.Initial(overshoot).Quality, Is.EqualTo(1f), "baseline 도 [0,1] clamp");
		}

		[Test]
		public void StageOrder_IndependentOfKind_InPhase0()
		{
			// Phase 0 = 단계 Kind 별 가중치 X (Phase 1+ 에서 SO 로 들어옴). Kind 만 바뀌고 Approach 동일이면 결과 동일.
			RefiningCoefficients coefficients = DefaultCoefficients();
			List<RefiningStage> order1 = new()
			{
				new RefiningStage(RefiningStageKind.Dissection, RefiningApproach.Careful),
				new RefiningStage(RefiningStageKind.Purification, RefiningApproach.Careful),
				new RefiningStage(RefiningStageKind.Refinement, RefiningApproach.Careful),
			};
			List<RefiningStage> order2 = new()
			{
				new RefiningStage(RefiningStageKind.Refinement, RefiningApproach.Careful),
				new RefiningStage(RefiningStageKind.Dissection, RefiningApproach.Careful),
				new RefiningStage(RefiningStageKind.Purification, RefiningApproach.Careful),
			};

			RefiningState s1 = RefiningChain.Evaluate(order1, coefficients);
			RefiningState s2 = RefiningChain.Evaluate(order2, coefficients);

			Assert.That(s1.Quality, Is.EqualTo(s2.Quality), "Phase 0 = Kind 가중치 0, 순서 무관");
			Assert.That(s1.Warmth, Is.EqualTo(s2.Warmth));
		}

		private static List<RefiningStage> MakeChain(RefiningApproach approach, int count)
		{
			List<RefiningStage> stages = new(count);
			for (int i = 0; i < count; i++)
			{
				// Kind 는 순환(Phase 0 = Kind 가중치 X, 그저 슬롯).
				RefiningStageKind kind = (RefiningStageKind)(i % 3);
				stages.Add(new RefiningStage(kind, approach));
			}
			return stages;
		}
	}
}
