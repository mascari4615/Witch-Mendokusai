using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// Weather 디버그 HUD (IMGUI). 현재 weather + 다음 후보 + 가중치 + Force Set 버튼.
	// WeatherSystem.OnWeatherChanged 첫 사용처 — 데드 인터페이스 방지. (TASK-WM-054-D D4)
	public class WeatherHUD : MonoBehaviour
	{
		[SerializeField] private bool show = true;
		[SerializeField] private Vector2 anchor = new Vector2(10, 150);

		private string lastChangeEvent = "(none)";
		private GUIStyle labelStyle;
		private GUIStyle headerStyle;

		private void OnEnable()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
				return;

			weatherSystem.OnWeatherChanged += HandleWeatherChanged;
		}

		private void OnDisable()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
				return;

			weatherSystem.OnWeatherChanged -= HandleWeatherChanged;
		}

		private void HandleWeatherChanged(WeatherType type) => lastChangeEvent = $"→ {type}";

		private void OnGUI()
		{
			if (show == false)
				return;

			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
				return;

			EnsureStyles();

			float width = 340f;
			float height = 280f;
			Rect rect = new Rect(anchor.x, anchor.y, width, height);
			GUI.Box(rect, GUIContent.none);

			GUILayout.BeginArea(new Rect(anchor.x + 10, anchor.y + 8, width - 20, height - 16));

			GUILayout.Label("<b>Weather</b>", headerStyle);
			GUILayout.Label($"current: {weatherSystem.Current}", labelStyle);
			GUILayout.Label($"next preview: {weatherSystem.PreviewNext()}", labelStyle);
			GUILayout.Label($"last: {lastChangeEvent}", labelStyle);

			DrawCurrentBucketWeights(weatherSystem);

			GUILayout.Space(4);
			GUILayout.Label("Force Set:", labelStyle);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Clear", GUILayout.Width(56)))
				weatherSystem.ForceSet(WeatherType.Clear);
			if (GUILayout.Button("Cloudy", GUILayout.Width(56)))
				weatherSystem.ForceSet(WeatherType.Cloudy);
			if (GUILayout.Button("Rain", GUILayout.Width(56)))
				weatherSystem.ForceSet(WeatherType.Rain);
			if (GUILayout.Button("Storm", GUILayout.Width(56)))
				weatherSystem.ForceSet(WeatherType.Storm);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Snow", GUILayout.Width(56)))
				weatherSystem.ForceSet(WeatherType.Snow);
			if (GUILayout.Button("Fog", GUILayout.Width(56)))
				weatherSystem.ForceSet(WeatherType.Fog);
			if (GUILayout.Button("Magical", GUILayout.Width(80)))
				weatherSystem.ForceTriggerMagical();
			GUILayout.EndHorizontal();

			GUILayout.EndArea();
		}

		private void DrawCurrentBucketWeights(WeatherSystem weatherSystem)
		{
			if (weatherSystem.Table == null)
				return;

			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return;

			int hour = worldClock.Hour;
			int season = worldClock.Season;
			int bucket = WeatherTransitionTableSO.HourToBucket(hour);

			GUILayout.Space(4);
			GUILayout.Label($"<b>profile</b> S{season} bucket{bucket} (h{hour})", headerStyle);

			List<WeatherTransitionTableSO.WeatherWeight> weights = weatherSystem.Table.GetWeights(hour, season);
			if (weights == null || weights.Count == 0)
			{
				GUILayout.Label("(no profile)", labelStyle);
				return;
			}

			foreach (WeatherTransitionTableSO.WeatherWeight entry in weights)
				GUILayout.Label($"  {entry.Type,-8}  {entry.Weight:F2}", labelStyle);
		}

		private void EnsureStyles()
		{
			if (labelStyle != null)
				return;

			labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
			headerStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, fontStyle = FontStyle.Bold };
		}
	}
}
