using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(WorldClockSO), menuName = "WM/WorldClockSO")]
	public class WorldClockSO : ScriptableObject
	{
		[field: Header("_" + nameof(WorldClockSO))]
		[field: Tooltip("1초 (실시간) 당 진행되는 게임 분. 1 이면 1게임일 = 24실분. 서버 WorldHost.MINUTES_PER_REAL_SECOND 와 같게")]
		[field: SerializeField, Range(0.1f, 600f)]
		public float MinutesPerRealSecond { get; set; } = 1f;

		[field: Header("시작 시각")]
		[field: SerializeField, Range(0, 23)] public int StartHour { get; set; } = 6;
		[field: SerializeField, Range(0, 59)] public int StartMinute { get; set; } = 0;

		[field: Header("길이")]
		[field: SerializeField, Range(1, 48)] public int HoursPerDay { get; set; } = 24;
		[field: SerializeField, Range(1, 100)] public int DaysPerSeason { get; set; } = 28;
		[field: SerializeField, Range(1, 12)] public int SeasonsPerYear { get; set; } = 4;

		/// <summary>이 설정의 자릿수와 시작 시각으로 새 달력. 달력 생성의 단일 경로 (같은 수 두 곳 금지).</summary>
		// 하루 길이를 줄였을 때 시작 시각이 하루 밖으로 나가지 않게
		private void OnValidate()
		{
			if (StartHour >= HoursPerDay)
				StartHour = HoursPerDay - 1;
		}

		public WorldCalendar CreateCalendar() => new WorldCalendar(HoursPerDay, DaysPerSeason, SeasonsPerYear, StartHour, StartMinute);
	}
}
