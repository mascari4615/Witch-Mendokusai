namespace WitchMendokusai
{
	// 하루치 생산 주문 — 한 레시피(ProductionRecipe)를 건물 Count 동 분량으로. CitySimulationSystem 입력 단위.
	// 같은 존타입 N동 = rate×N 집계 평가(도시 레벨 GlassBox — 동별 순차 아닌 균일 가동률).
	public readonly struct ProductionOrder
	{
		public readonly ProductionRecipe Recipe;
		public readonly int Count;

		public ProductionOrder(ProductionRecipe recipe, int count)
		{
			Recipe = recipe;
			Count = count;
		}
	}
}
