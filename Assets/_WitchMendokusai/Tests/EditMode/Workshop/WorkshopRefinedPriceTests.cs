using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Refining;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-172 × TASK-WM-170 — 정련한 상품의 값이 어떻게 정해지는지.
	/// 특히 <b>온기가 값에 안 섞이는지</b>를 지킨다 — 그건 사용자가 정할 무게라 코드가 먼저 정하면 안 된다.
	/// </summary>
	public class WorkshopRefinedPriceTests
	{
		// 정성껏 하면 품질이 많이 오르고 온기도 오른다 / 함부로 하면 품질이 조금 오르고 온기가 내린다.
		private static RefiningCoefficients Coefficients()
		{
			return new RefiningCoefficients(
				initialQuality: 0.3f,
				fastQualityDelta: 0.05f,
				carefulQualityDelta: 0.2f,
				fastWarmthDelta: -0.3f,
				carefulWarmthDelta: 0.3f);
		}

		private static List<RefiningStage> Stages(RefiningApproach approach, int count)
		{
			List<RefiningStage> stages = new List<RefiningStage>();
			for (int index = 0; index < count; index++)
			{
				stages.Add(new RefiningStage(RefiningStageKind.Purification, approach));
			}

			return stages;
		}

		[Test]
		public void 정련_단계가_없으면_기본가_그대로다()
		{
			Assert.AreEqual(100, WorkshopRefinedPrice.Evaluate(100, null, Coefficients(), 1f));
			Assert.AreEqual(100, WorkshopRefinedPrice.Evaluate(100, new List<RefiningStage>(), Coefficients(), 1f));
		}

		[Test]
		public void 정성껏_정련하면_함부로_한_것보다_비싸다()
		{
			int careful = WorkshopRefinedPrice.Evaluate(100, Stages(RefiningApproach.Careful, 2), Coefficients(), 1f);
			int fast = WorkshopRefinedPrice.Evaluate(100, Stages(RefiningApproach.Fast, 2), Coefficients(), 1f);

			Assert.Greater(careful, fast);
			Assert.Greater(fast, 100); // 함부로 해도 안 하는 것보단 낫다(품질이 오르긴 한다).
		}

		[Test]
		public void 품질보정이_0_이면_정련해도_값이_안_움직인다()
		{
			int price = WorkshopRefinedPrice.Evaluate(100, Stages(RefiningApproach.Careful, 3), Coefficients(), 0f);
			Assert.AreEqual(100, price);
		}

		[Test]
		public void 온기는_값에_안_섞인다()
		{
			// 품질 변화는 같게 두고 온기만 정반대로 바꾼 두 계수 — 값이 같아야 한다.
			RefiningCoefficients warm = new RefiningCoefficients(0.3f, 0.1f, 0.1f, 0.5f, 0.5f);
			RefiningCoefficients cold = new RefiningCoefficients(0.3f, 0.1f, 0.1f, -0.5f, -0.5f);

			List<RefiningStage> stages = Stages(RefiningApproach.Careful, 2);

			Assert.AreEqual(
				WorkshopRefinedPrice.Evaluate(100, stages, warm, 1f),
				WorkshopRefinedPrice.Evaluate(100, stages, cold, 1f));

			// 그렇다고 온기를 안 재는 건 아니다 — 이야기가 읽을 수 있게 따로 내준다.
			Assert.Greater(
				WorkshopRefinedPrice.Outcome(stages, warm).Warmth,
				WorkshopRefinedPrice.Outcome(stages, cold).Warmth);
		}

		[Test]
		public void 값은_정수이고_음수로_안_내려간다()
		{
			int price = WorkshopRefinedPrice.Evaluate(0, Stages(RefiningApproach.Careful, 1), Coefficients(), 1f);
			Assert.AreEqual(0, price);
		}
	}
}
