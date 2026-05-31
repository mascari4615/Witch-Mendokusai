namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 듀얼루프 브리지 코어: "골드 누계 투자량 → 낮 채집 효율" 순수 함수.
	/// MonoBehaviour/VContainer/PlayMode 0 — new() 직접. <see cref="WorkshopLedger"/> 가 호출.
	///
	/// 식: efficiency = clamp(base + floor(invested / goldPerStep) * efficiencyPerStep, .., maxEfficiency)
	/// floor 로 step 화 → 정수 산술 결정성 + UI 에서 "n step 도달" 표현 직관.
	/// </summary>
	public static class DayEfficiencyModel
	{
		public static float Evaluate(int goldInvested, DayEfficiencyCoefficients coefficients)
		{
			if (goldInvested <= 0 || coefficients.GoldPerEfficiencyStep <= 0f)
			{
				return Clamp(coefficients.BaseEfficiency, coefficients);
			}

			int steps = (int)(goldInvested / coefficients.GoldPerEfficiencyStep);
			float raw = coefficients.BaseEfficiency + steps * coefficients.EfficiencyPerStep;
			return Clamp(raw, coefficients);
		}

		/// <summary>base 채집량 × 효율 → 실제 수확량(정수, 내림). 음수 효율은 0 처리.</summary>
		public static int ScaleCollection(int baseAmount, float efficiency)
		{
			if (baseAmount <= 0 || efficiency <= 0f)
			{
				return 0;
			}

			return (int)(baseAmount * efficiency);
		}

		private static float Clamp(float value, DayEfficiencyCoefficients coefficients)
		{
			if (value > coefficients.MaxEfficiency)
			{
				return coefficients.MaxEfficiency;
			}

			return value;
		}
	}
}
