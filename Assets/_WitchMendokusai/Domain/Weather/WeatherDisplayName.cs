using System;

namespace WitchMendokusai
{
	/// <summary> 날씨의 화면 표시 이름 한 자리 </summary>
	public static class WeatherDisplayName
	{
		public static string Of(WeatherType weatherType) => weatherType switch
		{
			WeatherType.Clear => "맑음",
			WeatherType.Cloudy => "흐림",
			WeatherType.Rain => "비",
			WeatherType.Storm => "폭풍",
			WeatherType.Snow => "눈",
			WeatherType.Fog => "안개",
			WeatherType.Magical => "마력",
			_ => throw new ArgumentOutOfRangeException(nameof(weatherType), weatherType, "표시 이름이 없는 날씨"),
		};
	}
}
