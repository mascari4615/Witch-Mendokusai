using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 게임 내부 날씨 매니저. WorldClock.OnHourChanged hook 으로 자동 전이.
	// 시각 풀세트 (rain/snow particle / wet shader / SFX) 는 sub-E (WeatherDirector) 가 OnWeatherChanged event 구독해 페이드.
	// 본 Singleton 은 *데이터 + 전이 로직* 만 — 게임플레이 hook (sub-F) 도 OnWeatherChanged 사용처.
	// (TASK-WM-054-D D3)
	public class WeatherSystem : Singleton<WeatherSystem>
	{
		[field: SerializeField] public WeatherTransitionTableSO Table { get; private set; }

		[field: Tooltip("게임 시작 시 weather (Bootstrap default = Clear)")]
		[field: SerializeField] public WeatherType StartWeather { get; private set; } = WeatherType.Clear;

		public WeatherType Current { get; private set; }

		// payload 채널 — 새 weather 직접 전달 (sub-E 시각 / sub-F gameplay 구독)
		public event Action<WeatherType> OnWeatherChanged = delegate { };

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void EnsureSingletonOnPlay()
		{
			_ = Instance;
		}

		protected override void Awake()
		{
			base.Awake();
			Current = StartWeather;
		}

		private void Start()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
			{
				Debug.LogWarning($"[{nameof(WeatherSystem)}] WorldClock 미발견 — OnHourChanged hook 미등록");
				return;
			}

			worldClock.OnHourChanged += OnHourTick;
		}

		protected override void OnDestroy()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == true)
				worldClock.OnHourChanged -= OnHourTick;

			base.OnDestroy();
		}

		private void OnHourTick(int hour)
		{
			if (Table == null)
				return;

			WeatherType next = Table.RollNext(hour);
			if (next == Current)
				return;

			SetWeather(next);
		}

		private void SetWeather(WeatherType type)
		{
			Current = type;
			OnWeatherChanged.Invoke(type);
		}

		// sub-D D5 — Alisa NPC 일기예보 hook (sub-H 사용처).
		// 다음 hour 의 dominant weather 반환.
		public WeatherType PreviewNext()
		{
			if (Table == null)
				return WeatherType.Clear;

			if (WorldClock.TryGetExistingInstance(out WorldClock worldClock) == false)
				return WeatherType.Clear;

			int nextHour = (worldClock.Hour + 1) % worldClock.Config.HoursPerDay;
			return Table.DominantAt(nextHour);
		}

		// 디버그 / sub-G 의식 호출처.
		[ContextMenu("Debug/Force Set Clear")]
		private void DebugForceClear() => ForceSet(WeatherType.Clear);

		[ContextMenu("Debug/Force Set Rain")]
		private void DebugForceRain() => ForceSet(WeatherType.Rain);

		[ContextMenu("Debug/Force Set Storm")]
		private void DebugForceStorm() => ForceSet(WeatherType.Storm);

		[ContextMenu("Debug/Force Set Snow")]
		private void DebugForceSnow() => ForceSet(WeatherType.Snow);

		public void ForceSet(WeatherType type) => SetWeather(type);

		// sub-G — 마도서 의식 발동. 일반 가중치 행렬에서 Magical 0 이라 본 API 만이 발동 경로.
		public void ForceTriggerMagical() => SetWeather(WeatherType.Magical);
	}
}
