using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	// 게임 내부 시각 (시·분·일·계절·년) 모델. TimeManager.OnTick 에 hook 해서 advance.
	// SO 값 캐싱 X — 인스펙터 런타임 변경 즉시 반영. (TASK-WM-054-A)
	public class WorldClock : MonoBehaviour, IAuthorityAware
	{
		public static WorldClock Instance { get; private set; }

		public static bool TryGetExistingInstance(out WorldClock mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private TimeManager timeManager;

		[Inject]
		public void Construct(TimeManager timeManager)
		{
			this.timeManager = timeManager;
		}

		public Authority RequiredAuthority => Authority.Server;

		[field: SerializeField] public WorldClockSO Config { get; private set; }

		public int Year { get; private set; }
		public int Season { get; private set; }
		public int Day { get; private set; }
		public int Hour { get; private set; }
		public int Minute { get; private set; }

		// payload 채널 — 변경된 새 값 직접 전달
		public event Action<int> OnMinuteChanged = delegate { };
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
			timeManager.RegisterCallback(AdvanceTick);
		}

		private void OnDestroy()
		{
			if (timeManager != null)
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
			// 세계가 시각을 알려주면 그걸 따른다 (TASK-WM-217) — 시계는 세계의 것이지 내 것이 아니다.
			// 못 받는 동안에는 아래처럼 스스로 흘린다(접속 전 타이틀·에디터 단독 실행).
			if (TryFollowWorldTime() == true)
				return;

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
			// Hour/Day/Season 포함 모든 상태 갱신 후 발화 — ViewModel이 풀 상태 읽기 가능
			OnMinuteChanged.Invoke(Minute);
		}

		/// <summary>
		/// 세계가 준 시각으로 맞춘다 (TASK-WM-217). 값이 실제로 바뀐 것만 알린다 —
		/// 세계는 초당 20번 말하지만, 「시가 바뀌었다」는 한 번만 일어난 일이다.
		/// </summary>
		private bool TryFollowWorldTime()
		{
			WorldTimeView time = WorldDoor.Current?.Time;
			if (time == null)
				return false;

			bool minuteChanged = Minute != time.minute;
			bool hourChanged = Hour != time.hour;
			bool dayChanged = Day != time.day;
			bool seasonChanged = Season != time.season;

			// 상태를 전부 맞춘 뒤에 알린다 — 구독자가 풀 상태를 읽는다(WM-189 에서 얻은 순서).
			Year = time.year;
			Season = time.season;
			Day = time.day;
			Hour = time.hour;
			Minute = time.minute;
			minuteAccumulator = 0f;

			if (minuteChanged == true)
				OnMinuteChanged.Invoke(Minute);

			if (hourChanged == true)
				OnHourChanged.Invoke(Hour);

			if (dayChanged == true)
				OnDayChanged.Invoke(Day);

			if (seasonChanged == true)
				OnSeasonChanged.Invoke(Season);

			return true;
		}

		/// <summary>
		/// 행동이 먹은 시간만큼 시계를 앞으로 민다 (TASK-WM-410) — 「밭을 갈면 한 시간이 간다」.
		///
		/// ★ 왜 세계가 시각을 줄 때는 거절하나: 그때 하늘의 주인은 세계(서버)다.
		///   여기서 몰래 밀면 다음 틱에 세계 값으로 덮여 <b>시간이 되감긴 것처럼</b> 보인다.
		///   그 경우 시간 소비는 세계에 요청해야 할 일이라 여기서는 false 만 돌려준다.
		/// </summary>
		public bool AdvanceMinutes(int minutes)
		{
			if (minutes <= 0)
				return false;

			if (WorldDoor.Current?.Time != null)
				return false;

			ApplyMinutes(minutes);
			OnMinuteChanged.Invoke(Minute);
			return true;
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

			// Year 를 OnSeasonChanged 발화 *전*에 갱신 — 구독자(네트워크 sync 브리지)가
			// 풀 상태 스냅샷을 읽을 때 Year/Season 정합. 이전엔 발화 후 증가 → 스냅샷
			// Year 가 1계절 lag (2-peer 스모크가 Year 동기 불일치로 포착, WM-189).
			if (yearCarry > 0)
				Year += yearCarry;

			OnSeasonChanged.Invoke(Season);
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
