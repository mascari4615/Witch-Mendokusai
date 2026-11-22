using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	// 날씨마다 환경음 키가 FMOD 프로젝트의 실제 이벤트인지. 예전 키 (weather_clear 등) 는 이벤트가 없어 무음과 경고만 냈음
	public class WeatherAmbienceTest
	{
		[Test]
		public void EveryWeatherAmbienceIsRealEvent()
		{
			HashSet<string> paths = FmodProjectPaths.Load();
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(WeatherSO));
			Assert.IsNotEmpty(guids, nameof(WeatherSO));
			foreach (string guid in guids)
			{
				WeatherSO weather = AssetDatabase.LoadAssetAtPath<WeatherSO>(AssetDatabase.GUIDToAssetPath(guid));
				Assert.IsTrue(paths.Contains(weather.SfxKey), weather.name + ": " + weather.SfxKey);
			}
		}
	}
}
