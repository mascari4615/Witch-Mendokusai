using UnityEngine;

namespace WitchMendokusai
{
	// 전투 효과 끄기 설정. 화면 메뉴는 설정 창 쪽에서 이 값을 읽고 쓴다. 기본은 전부 켜짐
	public static class CombatEffectSettings
	{
		private const string DAMAGE_NUMBERS_KEY = "WM.CombatEffect.DamageNumbers";
		private const string FLASH_KEY = "WM.CombatEffect.Flash";
		private const string SCREEN_SHAKE_KEY = "WM.CombatEffect.ScreenShake";
		private const string REMAINS_LINGER_KEY = "WM.CombatEffect.RemainsLinger";

		public static bool DamageNumbers
		{
			get => PlayerPrefs.GetInt(DAMAGE_NUMBERS_KEY, 1) == 1;
			set => PlayerPrefs.SetInt(DAMAGE_NUMBERS_KEY, value ? 1 : 0);
		}

		public static bool Flash
		{
			get => PlayerPrefs.GetInt(FLASH_KEY, 1) == 1;
			set => PlayerPrefs.SetInt(FLASH_KEY, value ? 1 : 0);
		}

		public static bool ScreenShake
		{
			get => PlayerPrefs.GetInt(SCREEN_SHAKE_KEY, 1) == 1;
			set => PlayerPrefs.SetInt(SCREEN_SHAKE_KEY, value ? 1 : 0);
		}

		// 싸움 흔적 (잔해, 자국, 액체) 유지 선택지 번호. 값 목록은 RemainsSettings.LingerChoices
		// 처치마다 읽혀서 PlayerPrefs 대신 캐시. 바꿀 때 같이 갱신
		private static int cachedLingerChoice = -1;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetCache() => cachedLingerChoice = -1;

		public static int RemainsLingerChoice
		{
			get
			{
				if (cachedLingerChoice < 0)
					cachedLingerChoice = PlayerPrefs.GetInt(REMAINS_LINGER_KEY, RemainsSettings.Load().DefaultLingerChoice);
				return cachedLingerChoice;
			}
			set
			{
				cachedLingerChoice = value;
				PlayerPrefs.SetInt(REMAINS_LINGER_KEY, value);
			}
		}

		public static float RemainsLingerSeconds => RemainsSettings.Load().LingerSecondsAt(RemainsLingerChoice);
	}
}
