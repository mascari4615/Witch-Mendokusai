using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 시간/날씨 디버그·조정 모드 — DevWindow 안 IDevMode (TASK-WM-096, 2026-05-10).
	/// 옛 OnGUI HUD 2 개 (WorldClockHUD + WeatherHUD) 의 UI Toolkit 마이그.
	/// Time 섹션 = 속도 슬라이더 + Skip / Weather 섹션 = current·preview + Force Set 7 + bucket weights.
	/// </summary>
	public class TimeWeatherMode : IDevMode
	{
		private const long REFRESH_INTERVAL_MS = 250;
		private static readonly WeatherType[] FORCE_SET_TYPES =
		{
			WeatherType.Clear, WeatherType.Cloudy, WeatherType.Rain, WeatherType.Storm,
			WeatherType.Snow, WeatherType.Fog,
		};

		public string Id => "timeweather";
		public string DisplayName => "Time/Weather";
		public VisualElement Root { get; }

		private readonly Label timeStatusLabel;
		private readonly Label timeMetaLabel;
		private readonly Label speedLabel;
		private readonly Slider speedSlider;
		private readonly Label weatherCurrentLabel;
		private readonly Label weatherPreviewLabel;
		private readonly Label weatherLastLabel;
		private readonly Label bucketHeaderLabel;
		private readonly VisualElement bucketWeightsContainer;

		private string lastHourEvent = "(none)";
		private string lastWeatherEvent = "(none)";
		private IVisualElementScheduledItem refreshSchedule;

		public TimeWeatherMode()
		{
			ScrollView scroll = new();
			scroll.style.flexGrow = 1;
			Root = scroll;

			// Time 섹션
			Label timeHeader = new("<b>Time</b>") { enableRichText = true };
			timeHeader.style.fontSize = 14;
			timeHeader.style.marginBottom = 4;
			scroll.Add(timeHeader);

			timeStatusLabel = new Label { enableRichText = true };
			timeStatusLabel.style.marginBottom = 2;
			scroll.Add(timeStatusLabel);

			timeMetaLabel = new Label();
			timeMetaLabel.style.fontSize = 11;
			timeMetaLabel.style.marginBottom = 8;
			scroll.Add(timeMetaLabel);

			speedLabel = new Label();
			speedLabel.style.fontSize = 11;
			scroll.Add(speedLabel);

			speedSlider = new Slider(0.1f, 600f) { showInputField = true };
			speedSlider.style.marginBottom = 8;
			speedSlider.RegisterValueChangedCallback(OnSpeedChanged);
			scroll.Add(speedSlider);

			Button skipButton = new(OnSkipDayClicked) { text = "Skip to next day" };
			skipButton.style.marginBottom = 16;
			scroll.Add(skipButton);

			// Weather 섹션
			Label weatherHeader = new("<b>Weather</b>") { enableRichText = true };
			weatherHeader.style.fontSize = 14;
			weatherHeader.style.marginBottom = 4;
			scroll.Add(weatherHeader);

			weatherCurrentLabel = new Label();
			weatherCurrentLabel.style.marginBottom = 2;
			scroll.Add(weatherCurrentLabel);

			weatherPreviewLabel = new Label();
			weatherPreviewLabel.style.marginBottom = 2;
			scroll.Add(weatherPreviewLabel);

			weatherLastLabel = new Label();
			weatherLastLabel.style.marginBottom = 8;
			scroll.Add(weatherLastLabel);

			Label forceSetLabel = new("Force Set:");
			forceSetLabel.style.fontSize = 11;
			forceSetLabel.style.marginBottom = 4;
			scroll.Add(forceSetLabel);

			VisualElement forceSetRow = new();
			forceSetRow.style.flexDirection = FlexDirection.Row;
			forceSetRow.style.flexWrap = Wrap.Wrap;
			forceSetRow.style.marginBottom = 4;
			scroll.Add(forceSetRow);

			foreach (WeatherType type in FORCE_SET_TYPES)
			{
				WeatherType captured = type;
				Button button = new(() => OnForceSetClicked(captured)) { text = type.ToString() };
				button.style.minWidth = 64;
				button.style.marginRight = 4;
				forceSetRow.Add(button);
			}

			Button magicalButton = new(OnForceMagicalClicked) { text = "Magical" };
			magicalButton.style.minWidth = 80;
			magicalButton.style.marginBottom = 12;
			scroll.Add(magicalButton);

			bucketHeaderLabel = new Label { enableRichText = true };
			bucketHeaderLabel.style.fontSize = 11;
			bucketHeaderLabel.style.marginBottom = 2;
			scroll.Add(bucketHeaderLabel);

			bucketWeightsContainer = new VisualElement();
			scroll.Add(bucketWeightsContainer);
		}

		public void OnActivate()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock))
				worldClock.OnHourChanged += HandleHourChanged;

			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem))
				weatherSystem.OnWeatherChanged += HandleWeatherChanged;

			Refresh();

			refreshSchedule = Root.schedule.Execute(Refresh).Every(REFRESH_INTERVAL_MS);
		}

		public void OnDeactivate()
		{
			refreshSchedule?.Pause();
			refreshSchedule = null;

			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock))
				worldClock.OnHourChanged -= HandleHourChanged;

			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem))
				weatherSystem.OnWeatherChanged -= HandleWeatherChanged;
		}

		private void HandleHourChanged(int hour) => lastHourEvent = $"hour → {hour}";
		private void HandleWeatherChanged(WeatherType type) => lastWeatherEvent = $"→ {type}";

		private void Refresh()
		{
			RefreshTime();
			RefreshWeather();
		}

		private void RefreshTime()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false || worldClock.Config == null)
			{
				timeStatusLabel.text = "WorldClock 없음";
				timeMetaLabel.text = string.Empty;
				speedLabel.text = string.Empty;
				return;
			}

			timeStatusLabel.text = worldClock.ToDebugString();
			timeMetaLabel.text = $"paused: {worldClock.IsClockPaused}  /  last: {lastHourEvent}";

			float speed = worldClock.Config.MinutesPerRealSecond;
			speedLabel.text = $"speed: {speed:F1} min/sec";

			if (Mathf.Approximately(speedSlider.value, speed) == false)
				speedSlider.SetValueWithoutNotify(speed);
		}

		private void RefreshWeather()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
			{
				weatherCurrentLabel.text = "WeatherSystem 없음";
				weatherPreviewLabel.text = string.Empty;
				weatherLastLabel.text = string.Empty;
				bucketHeaderLabel.text = string.Empty;
				bucketWeightsContainer.Clear();
				return;
			}

			weatherCurrentLabel.text = $"current: {weatherSystem.Current}";
			weatherPreviewLabel.text = $"next preview: {weatherSystem.PreviewNext()}";
			weatherLastLabel.text = $"last: {lastWeatherEvent}";

			RefreshBucketWeights(weatherSystem);
		}

		private void RefreshBucketWeights(WeatherSystem weatherSystem)
		{
			bucketWeightsContainer.Clear();

			if (weatherSystem.Table == null || WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
			{
				bucketHeaderLabel.text = string.Empty;
				return;
			}

			int hour = worldClock.Hour;
			int season = worldClock.Season;
			int bucket = WeatherTransitionTableSO.HourToBucket(hour);
			bucketHeaderLabel.text = $"<b>profile</b> S{season} bucket{bucket} (h{hour})";

			List<WeatherTransitionTableSO.WeatherWeight> weights = weatherSystem.Table.GetWeights(hour, season);
			if (weights == null || weights.Count == 0)
			{
				Label none = new("(no profile)");
				none.style.fontSize = 11;
				bucketWeightsContainer.Add(none);
				return;
			}

			foreach (WeatherTransitionTableSO.WeatherWeight entry in weights)
			{
				Label item = new($"  {entry.Type,-8}  {entry.Weight:F2}");
				item.style.fontSize = 11;
				bucketWeightsContainer.Add(item);
			}
		}

		private void OnSpeedChanged(ChangeEvent<float> evt)
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false || worldClock.Config == null)
				return;
			worldClock.Config.MinutesPerRealSecond = evt.newValue;
		}

		private void OnSkipDayClicked()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return;
			worldClock.SkipToNextDay();
		}

		private void OnForceSetClicked(WeatherType type)
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
				return;
			weatherSystem.ForceSet(type);
		}

		private void OnForceMagicalClicked()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
				return;
			weatherSystem.ForceTriggerMagical();
		}
	}
}
