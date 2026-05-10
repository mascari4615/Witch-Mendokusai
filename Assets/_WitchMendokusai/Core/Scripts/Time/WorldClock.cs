using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 게임 내부 시각 (시·분·일·계절·년) 모델. TimeManager.OnTick 에 hook 해서 advance.
	// SO 값 캐싱 X — 인스펙터 런타임 변경 즉시 반영. (TASK-WM-054-A)
	public class WorldClock : MonoBehaviour
	{
		public static WorldClock Instance { get; private set; }

		public static bool TryGetExistingInstance(out WorldClock mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		[field: SerializeField] public WorldClockSO Config { get; private set; }

		public int Year { get; private set; }
		public int Season { get; private set; }
		public int Day { get; private set; }
		public int Hour { get; private set; }
		public int Minute { get; private set; }

		// payload 채널 — 변경된 새 값 직접 전달
		public event Action<int> OnHourChanged = delegate { };
		public event Action<int> OnDayChanged = delegate { };
		public event Action<int> OnSeasonChanged = delegate { };

		// 시계 정지 (게임 전체 정지 X — TimeManager.Pause 와 분리, 시계만 멈춤)
		private readonly List<GameObject> pausers = new();
		public bool IsClockPaused => pausers.Count > 0;

		// TICK 단위 누적 sub-minute (실수 분량의 게임 분)
		private float minuteAccumulator;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			ResetToConfigStart();
		}

		private void Start()
		{
			TimeManager.Instance.RegisterCallback(AdvanceTick);
		}

		private void OnDestroy()
		{
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager) == true)
				timeManager.RemoveCallback(AdvanceTick);

			if (Instance == this)
				Instance = null;
		}

		private void ResetToConfigStart()
		{
			Year = 1;
			Season = 0;
			Day = 1;
			Hour = Config.StartHour;
			Minute = Config.StartMinute;
			minuteAccumulator = 0f;
		}

		private void AdvanceTick()
		{
			if (IsClockPaused == true)
				return;

			// SO 값 매 tick 다시 읽음 — 런타임 tweak 즉시 반영
			float deltaMinutes = Config.MinutesPerRealSecond * TimeManager.TICK;
			minuteAccumulator += deltaMinutes;

			int wholeMinutes = (int)minuteAccumulator;
			if (wholeMinutes <= 0)
				return;

			minuteAccumulator -= wholeMinutes;
			ApplyMinutes(wholeMinutes);
		}

		private void ApplyMinutes(int minutesToAdd)
		{
			int newMinute = Minute + minutesToAdd;
			int hourCarry = newMinute / 60;
			Minute = newMinute % 60;

			if (hourCarry <= 0)
				return;

			ApplyHours(hourCarry);
		}

		private void ApplyHours(int hoursToAdd)
		{
			int newHour = Hour + hoursToAdd;
			int dayCarry = newHour / Config.HoursPerDay;
			Hour = newHour % Config.HoursPerDay;

			OnHourChanged.Invoke(Hour);

			if (dayCarry <= 0)
				return;

			ApplyDays(dayCarry);
		}

		private void ApplyDays(int daysToAdd)
		{
			int newDay = Day + daysToAdd;
			int seasonCarry = (newDay - 1) / Config.DaysPerSeason;
			Day = ((newDay - 1) % Config.DaysPerSeason) + 1;

			OnDayChanged.Invoke(Day);

			if (seasonCarry <= 0)
				return;

			ApplySeasons(seasonCarry);
		}

		private void ApplySeasons(int seasonsToAdd)
		{
			int newSeason = Season + seasonsToAdd;
			int yearCarry = newSeason / Config.SeasonsPerYear;
			Season = newSeason % Config.SeasonsPerYear;

			OnSeasonChanged.Invoke(Season);

			if (yearCarry <= 0)
				return;

			Year += yearCarry;
		}

		public void PauseClock(GameObject pauser)
		{
			if (pauser == null)
				return;

			if (pausers.Contains(pauser) == true)
				return;

			pausers.Add(pauser);
		}

		public void ResumeClock(GameObject pauser)
		{
			if (pauser == null)
				return;

			pausers.Remove(pauser);
		}

		public void SkipTo(int targetHour)
		{
			if (targetHour < 0 || targetHour >= Config.HoursPerDay)
			{
				Debug.LogWarning($"[{nameof(WorldClock)}] SkipTo: invalid hour {targetHour}");
				return;
			}

			int hoursToAdd;
			if (targetHour > Hour)
				hoursToAdd = targetHour - Hour;
			else
				hoursToAdd = (Config.HoursPerDay - Hour) + targetHour;

			Minute = 0;
			ApplyHours(hoursToAdd);
		}

		public void SkipDays(int days)
		{
			if (days <= 0)
				return;

			ApplyDays(days);
		}

		[ContextMenu(nameof(SkipToNextDay))]
		public void SkipToNextDay() => SkipTo(Config.StartHour);

		[ContextMenu("Debug/Skip 1 Hour")]
		private void DebugSkipOneHour() => ApplyHours(1);

		public string ToDebugString() => $"Y{Year} S{Season + 1} D{Day} {Hour:D2}:{Minute:D2}";
	}
}
