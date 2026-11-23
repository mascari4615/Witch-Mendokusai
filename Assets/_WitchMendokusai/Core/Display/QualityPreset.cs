using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 그래픽 품질 (낮음, 중간, 높음). 값은 PlayerPrefs, 설정창 화면 탭의 품질 줄.
	/// 수치 (렌더 배율, MSAA, 그림자) 는 단계마다 연결된 URP 에셋이 정본이고 여기에는 단계와 Unity 품질 단계 이름의 대응만 둠.
	/// 단계를 바꾸면 Unity 가 그 단계의 수직 동기 값을 다시 넣으므로 바로 DisplaySettings.Apply 로 덮음 (순서 고정)
	/// </summary>
	public static class QualityPreset
	{
		private const string TIER_KEY = "WM.Quality";

		public const int TIER_LOW = 0;
		public const int TIER_MID = 1;
		public const int TIER_HIGH = 2;
		public const int TIER_COUNT = 3;

		/// <summary> 단계 번호에 대응하는 Unity 품질 단계 이름 (ProjectSettings/QualitySettings). 나머지 단계는 쓰지 않음 </summary>
		private static readonly string[] LEVEL_NAMES = { "Low", "Medium", "High" };

		/// <summary> 고른 적 없으면 중간. 성능 기준선 (SurvivalPerformanceScenario) 도 Medium </summary>
		public static int Tier
		{
			get => ClampTier(PlayerPrefs.GetInt(TIER_KEY, TIER_MID));
			set
			{
				PlayerPrefs.SetInt(TIER_KEY, ClampTier(value));
				PlayerPrefs.Save();
				Apply();
			}
		}

		/// <summary> 품질을 한 번이라도 골랐나. 안 골랐으면 부팅 때 Unity 기본 단계를 건드리지 않음 </summary>
		public static bool HasChoice => PlayerPrefs.HasKey(TIER_KEY);

		public static int ClampTier(int tier) => Mathf.Clamp(tier, TIER_LOW, TIER_HIGH);

		/// <summary> 단계에 대응하는 Unity 품질 단계 번호. 이름이 없으면 -1 </summary>
		public static int ResolveLevelIndex(string[] levelNames, int tier)
			=> Array.FindIndex(levelNames, levelName => string.Equals(levelName, LEVEL_NAMES[ClampTier(tier)], StringComparison.OrdinalIgnoreCase));

		public static void Apply()
		{
			// 에디터는 SetQualityLevel 이 QualitySettings.asset 의 현재 단계를 바꿔 남기므로 건너뜀 (수직 동기와 같은 이유)
			if (Application.isEditor == false)
			{
				int level = ResolveLevelIndex(QualitySettings.names, Tier);
				if (level < 0)
					throw new InvalidOperationException($"QualitySettings 에 '{LEVEL_NAMES[Tier]}' 단계가 없음");
				QualitySettings.SetQualityLevel(level, true);
			}
			// 단계가 정한 수직 동기를 설정값으로 되돌림. 이 줄이 단계 적용보다 앞서면 144FPS 목표가 깨짐
			DisplaySettings.Apply();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void ApplyOnBoot()
		{
			if (HasChoice)
				Apply();
		}
	}
}
