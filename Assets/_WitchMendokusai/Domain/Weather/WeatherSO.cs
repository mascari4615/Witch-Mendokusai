using UnityEngine;

namespace WitchMendokusai
{
	// 단일 날씨의 데이터·게임플레이 flag·페이드 시간.
	// 시각 풀세트 (rain/snow particle / wet shader / SFX) 는 sub-E (WeatherDirector) 가 별 prefab 로 처리.
	// 본 SO 는 *데이터* 만 — 게임플레이 hook (sub-F 농사 / 어종) + 디버그 표현 + sub-E 페이드 hook.
	// (TASK-WM-054-D D1)
	[CreateAssetMenu(fileName = nameof(WeatherSO), menuName = "WM/Weather/WeatherSO")]
	public class WeatherSO : DataSO
	{
		[field: Header("_" + nameof(WeatherSO))]
		[field: Tooltip("WeatherType enum 항목 (Bootstrap 이 자동 박음).")]
		[field: SerializeField] public WeatherType Type { get; private set; }

		[field: Tooltip("이 날씨로 페이드 인 시간 (게임분). 30 default = 자연 전이.")]
		[field: SerializeField, Range(1f, 120f)] public float FadeMinutes { get; private set; } = 30f;

		[field: Tooltip("ground 가 젖은 상태 (sub-F 농사 자동 물주기 / sub-E Wet shader hook 용).")]
		[field: SerializeField] public bool IsWet { get; private set; }

		[field: Tooltip("AudioManager 호출 키 (sub-E WeatherDirector 가 발동 시 사용).")]
		[field: SerializeField] public string SfxKey { get; private set; } = "";

		[field: Tooltip("디버그 HUD 표시 색 (sub-D D4).")]
		[field: SerializeField] public Color DebugTint { get; private set; } = Color.white;

		[field: Tooltip("시각 풀세트 prefab (sub-E E2). null 면 visual 없음 (Clear/Cloudy default). WeatherDirector 가 OnWeatherChanged 시 Instantiate.")]
		[field: SerializeField] public GameObject VisualPrefab { get; private set; }
	}
}
