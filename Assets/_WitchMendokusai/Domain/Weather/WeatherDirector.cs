using UnityEngine;

namespace WitchMendokusai
{
	// 시각 풀세트 매니저. WeatherSystem.OnWeatherChanged 구독 → 현재 WeatherSO 해석.
	// E1 단독 = Singleton + SO 해석 + Console log (sub-E 시각 검증 신호).
	// E2 후속 = particle layer 4 (Rain/Snow/Fog/Storm) instantiate + 페이드.
	// E3 후속 = wet shader (sub-C6 SetGlobal `_Wetness` 활용).
	// (TASK-WM-054-E E1)
	public class WeatherDirector : Singleton<WeatherDirector>
	{
		// 현재 적용된 WeatherSO — sub-E E2 가 sfxKey / IsWet flag 등을 읽음.
		public WeatherSO CurrentWeatherSO { get; private set; }
		public WeatherType LastApplied { get; private set; } = WeatherType.Clear;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void EnsureSingletonOnPlay()
		{
			_ = Instance;
		}

		private void Start()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == false)
			{
				Debug.LogWarning($"[{nameof(WeatherDirector)}] WeatherSystem 미발견 — OnWeatherChanged hook 미등록");
				return;
			}

			weatherSystem.OnWeatherChanged += HandleWeatherChanged;

			// 초기 sync — Start 가 WeatherSystem.Awake 보다 먼저 호출될 수도 있어 현재 weather 직접 적용.
			HandleWeatherChanged(weatherSystem.Current);
		}

		protected override void OnDestroy()
		{
			if (WeatherSystem.TryGetExistingInstance(out WeatherSystem weatherSystem) == true)
				weatherSystem.OnWeatherChanged -= HandleWeatherChanged;

			base.OnDestroy();
		}

		private void HandleWeatherChanged(WeatherType type)
		{
			LastApplied = type;
			CurrentWeatherSO = ResolveSO(type);

			string soName = CurrentWeatherSO != null ? CurrentWeatherSO.Name : "<not loaded>";
			Debug.Log($"[{nameof(WeatherDirector)}] applied {type} → SO '{soName}' (sfx={GetSfxKey()}, isWet={GetIsWet()})");
		}

		// Resources/Weather/{type}.asset 직접 로드 (캐싱 X — SO 값 런타임 변경 시 즉시 반영).
		public WeatherSO ResolveSO(WeatherType type) => Resources.Load<WeatherSO>($"Weather/{type}");

		private string GetSfxKey() => CurrentWeatherSO != null ? CurrentWeatherSO.SfxKey : "(none)";
		private bool GetIsWet() => CurrentWeatherSO != null && CurrentWeatherSO.IsWet;
	}
}
