using System;
using R3;
using VContainer;

namespace WitchMendokusai
{
    public sealed class WorldClockViewModel : IDisposable
    {
        private readonly WorldClock _worldClock;
        private readonly ReactiveProperty<string> _timeText;
        private readonly ReactiveProperty<string> _dateText;

        public Observable<string> TimeText => _timeText;
        public Observable<string> DateText => _dateText;

        [Inject]
        public WorldClockViewModel(WorldClock worldClock)
        {
            _worldClock = worldClock;
            _timeText = new ReactiveProperty<string>(FormatTime(worldClock.Hour, worldClock.Minute));
            _dateText = new ReactiveProperty<string>(FormatDate(worldClock.Season, worldClock.Day));

            worldClock.OnMinuteChanged += OnMinuteChanged;
            worldClock.OnDayChanged += OnDayChanged;
            worldClock.OnSeasonChanged += OnSeasonChanged;
        }

        private void OnMinuteChanged(int minute)
        {
            _timeText.Value = FormatTime(_worldClock.Hour, minute);
        }

        private void OnDayChanged(int day)
        {
            _dateText.Value = FormatDate(_worldClock.Season, day);
        }

        private void OnSeasonChanged(int season)
        {
            _dateText.Value = FormatDate(season, _worldClock.Day);
        }

        private static string FormatTime(int hour, int minute) => $"{hour:D2}:{minute:D2}";
        private static string FormatDate(int season, int day) => $"S{season + 1} D{day}";

        public void Dispose()
        {
            _worldClock.OnMinuteChanged -= OnMinuteChanged;
            _worldClock.OnDayChanged -= OnDayChanged;
            _worldClock.OnSeasonChanged -= OnSeasonChanged;
            _timeText.Dispose();
            _dateText.Dispose();
        }
    }
}
