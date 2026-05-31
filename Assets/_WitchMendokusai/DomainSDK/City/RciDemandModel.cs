using UnityEngine;

namespace WitchMendokusai
{
	// RCI 3변수 수요 피드백 — 순수 함수(상태 0, new() 후 Evaluate 만). GlassBox 고전 균형식:
	//  주거 R = 외부 이주 baseline + 일자리 총량(c+i)이 부양 가능한 주민 대비 현재 주민(r) 부족분
	//           → 빈 도시도 immigration baseline 으로 R+(부트스트랩), 일자리 많으면 R 더 +.
	//  상업 C = 현재 주민(r)이 부양 가능한 상업 대비 현재 상업(c) 부족분 → 인구 많으면 C+.
	//  산업 I = 외부 수출 baseline + 주민 대비 산업 부족분 → 빈 도시도 baseline 으로 I+(부트스트랩).
	// 셋이 서로 균형: 빈 도시 = R(이주)+I(수출) 부트스트랩 → r↑ → C↑, i↑ → 일자리↑ → R↑ 순환. 각 항 clamp[-1,1].
	//
	// ★ 입력 r/c/i = "현재 점유(지은 건물) 수"여야 함 — "칠한 존 칸 수"(capacity)가 아니라 occupancy.
	//    capacity 를 넣으면 주거칸 ≫ 일자리칸인 자연스러운 도시에서 residentialGap 영구 음수 → 주거 미성장.
	//    호출자(CityPaintManager.OnDayChanged)가 GridData 건물 수를 존타입별로 집계해 주입.
	//
	// 비전-중립 — 마법진/사역마 스킨 무관(순수 수학). 계수는 RciDemandCoefficients 주입(수치 노출).
	public sealed class RciDemandModel
	{
		// 수요 정규화 범위 — RciDemand 출력 계약(+성장압/-쇠퇴압). 튜닝 수치 아닌 타입 범위 상수.
		private const float DEMAND_MIN = -1f;
		private const float DEMAND_MAX = 1f;

		public RciDemand Evaluate(int residential, int commercial, int industrial, RciDemandCoefficients coefficients)
		{
			int jobs = commercial + industrial;

			// 주거: 외부 이주 baseline + 일자리가 부양 가능한 주민 - 현재 주민.
			float residentialGap = coefficients.ImmigrationBaseline + jobs * coefficients.ResidentsPerJob - residential;
			float demandResidential = Mathf.Clamp(residentialGap * coefficients.DemandGain, DEMAND_MIN, DEMAND_MAX);

			// 상업: 주민이 요구하는 상업 - 현재 상업.
			float commercialGap = residential * coefficients.ShopsPerResident - commercial;
			float demandCommercial = Mathf.Clamp(commercialGap * coefficients.DemandGain, DEMAND_MIN, DEMAND_MAX);

			// 산업: 외부 수출 + 주민이 요구하는 산업 - 현재 산업.
			float industrialGap = coefficients.ExportBaseline + residential * coefficients.IndustryPerResident - industrial;
			float demandIndustrial = Mathf.Clamp(industrialGap * coefficients.DemandGain, DEMAND_MIN, DEMAND_MAX);

			return new RciDemand(demandResidential, demandCommercial, demandIndustrial);
		}
	}
}
