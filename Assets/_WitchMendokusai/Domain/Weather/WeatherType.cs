namespace WitchMendokusai
{
	// 날씨 enum. 6+1 (Magical = lore 결합 — sub-G 마도서 의식 발동).
	// 가중치 행렬 (sub-D D2) 의 *current* + *next* 축 정의.
	// (TASK-WM-054-D D1)
	public enum WeatherType
	{
		Clear = 0,
		Cloudy = 1,
		Rain = 2,
		Storm = 3,
		Snow = 4,
		Fog = 5,
		Magical = 6,
	}
}
