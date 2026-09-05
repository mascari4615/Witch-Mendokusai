namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 듀얼루프 브리지 계수: "밤 골드 → 다음 낮 채집 효율 증대" 환산식.
	/// 순수 값(POCO) — <see cref="DayEfficiencyModel"/> 가 주입받아 평가. 게임 배선에선 SO 가 공급.
	///
	/// 디폴트값 없음 — 공급자가 명시(하드코딩 회피, 같은 수치 두 곳 박기 X). City RciDemandCoefficients 동형.
	/// </summary>
	public readonly struct DayEfficiencyCoefficients
	{
		/// <summary>투자 0 일 때 기준 효율 (보통 1.0 — 채집 베이스 그대로).</summary>
		public readonly float BaseEfficiency;

		/// <summary>1 step 효율 증가에 필요한 골드 (낮을수록 빠른 성장 — 시드라 매끄러운 curve 아니어도 OK).</summary>
		public readonly float GoldPerEfficiencyStep;

		/// <summary>1 step 당 효율에 더해지는 양 (0.2 = 매 step 마다 +20%).</summary>
		public readonly float EfficiencyPerStep;

		/// <summary>효율 상한 — 무한 인플레 차단. 가격·재료 밸런스 깨짐 방지.</summary>
		public readonly float MaxEfficiency;

		public DayEfficiencyCoefficients(float baseEfficiency, float goldPerEfficiencyStep, float efficiencyPerStep, float maxEfficiency)
		{
			BaseEfficiency = baseEfficiency;
			GoldPerEfficiencyStep = goldPerEfficiencyStep;
			EfficiencyPerStep = efficiencyPerStep;
			MaxEfficiency = maxEfficiency;
		}
	}
}
