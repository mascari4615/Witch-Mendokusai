using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-164 Phase 1 step3 — <see cref="RciDemandModel"/> 수요 피드백식 회귀 잠금.
	///
	/// 순수 함수 — 부호/단조성/clamp/수렴 불변식만 검증(정확한 값 X, 계수 tuning 은 SO 영역).
	/// MonoBehaviour/VContainer/PlayMode 0 — new() 직접.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class RciDemandModelTest
	{
		// 테스트 계수 — 명시 주입(POCO 디폴트 없음). 게임에선 RciDemandSO 가 공급.
		private static RciDemandCoefficients Coeffs()
		{
			return new RciDemandCoefficients(
				residentsPerJob: 1f,
				shopsPerResident: 0.3f,
				industryPerResident: 0.2f,
				exportBaseline: 5f,
				demandGain: 0.1f);
		}

		[Test]
		public void EmptyCity_HasIndustryBaselineDemand()
		{
			RciDemandModel model = new();

			RciDemand demand = model.Evaluate(0, 0, 0, Coeffs());

			// 빈 도시: 주거·상업 수요 0(부양할 일자리·인구 없음), 산업만 외부수출 baseline 으로 + (부트스트랩).
			Assert.That(demand.Residential, Is.Zero, "빈 도시 = 주거 수요 0");
			Assert.That(demand.Commercial, Is.Zero, "빈 도시 = 상업 수요 0");
			Assert.That(demand.Industrial, Is.GreaterThan(0f), "빈 도시 = 산업 baseline 으로 첫 수요");
		}

		[Test]
		public void MoreJobs_IncreaseResidentialDemand()
		{
			RciDemandModel model = new();
			RciDemandCoefficients coeffs = Coeffs();

			// 일자리(c+i) 적을 때 vs 많을 때 — 주거 수요 단조 증가(일자리가 사람을 끌어들임).
			float fewJobs = model.Evaluate(0, 1, 0, coeffs).Residential;
			float manyJobs = model.Evaluate(0, 5, 0, coeffs).Residential;

			Assert.That(manyJobs, Is.GreaterThan(fewJobs), "일자리 많을수록 주거 수요↑");
		}

		[Test]
		public void MorePopulation_IncreaseCommercialDemand()
		{
			RciDemandModel model = new();
			RciDemandCoefficients coeffs = Coeffs();

			float fewPeople = model.Evaluate(1, 0, 0, coeffs).Commercial;
			float manyPeople = model.Evaluate(8, 0, 0, coeffs).Commercial;

			Assert.That(manyPeople, Is.GreaterThan(fewPeople), "인구 많을수록 상업 수요↑");
		}

		[Test]
		public void Oversupply_NegativeDemand()
		{
			RciDemandModel model = new();
			RciDemandCoefficients coeffs = Coeffs();

			// 주민은 많은데 일자리 0 → 주거 과잉(부양 못 함) → 주거 수요 음수(쇠퇴압).
			RciDemand demand = model.Evaluate(20, 0, 0, coeffs);

			Assert.That(demand.Residential, Is.LessThan(0f), "일자리 없이 주민만 많으면 주거 쇠퇴압");
		}

		[Test]
		public void Demand_AlwaysClampedToUnitRange()
		{
			RciDemandModel model = new();
			RciDemandCoefficients coeffs = Coeffs();

			// 극단값 — 거대 도시. 모든 수요 [-1,1] 안.
			RciDemand huge = model.Evaluate(10000, 10000, 10000, coeffs);
			RciDemand hugePop = model.Evaluate(99999, 0, 0, coeffs);

			foreach (float value in new[] { huge.Residential, huge.Commercial, huge.Industrial, hugePop.Residential, hugePop.Commercial, hugePop.Industrial })
			{
				Assert.That(value, Is.InRange(-1f, 1f), "수요는 항상 [-1,1] clamp");
			}
		}

		[Test]
		public void BalancedCity_DemandNearZero()
		{
			RciDemandModel model = new();
			RciDemandCoefficients coeffs = Coeffs();

			// 균형점 근사: 일자리(c+i=10)=주민(r=10) → 주거 gap 0. 상업 c=3 ≈ r*0.3=3 → 상업 gap 0.
			// 산업 i=7, baseline5 + r*0.2=2 = 7 → 산업 gap 0. 셋 다 ~0(수렴).
			RciDemand demand = model.Evaluate(10, 3, 7, coeffs);

			Assert.That(demand.Residential, Is.EqualTo(0f).Within(0.001f), "일자리=주민 → 주거 수렴");
			Assert.That(demand.Commercial, Is.EqualTo(0f).Within(0.001f), "상업=수요 → 상업 수렴");
			Assert.That(demand.Industrial, Is.EqualTo(0f).Within(0.001f), "산업=baseline+수요 → 산업 수렴");
		}
	}
}
