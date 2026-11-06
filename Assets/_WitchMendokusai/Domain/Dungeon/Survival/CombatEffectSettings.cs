using UnityEngine;

namespace WitchMendokusai
{
	// 전투 효과 끄기 설정. 화면 메뉴는 설정 창 쪽에서 이 값을 읽고 쓴다. 기본은 전부 켜짐
	public static class CombatEffectSettings
	{
		private const string DAMAGE_NUMBERS_KEY = "WM.CombatEffect.DamageNumbers";
		private const string FLASH_KEY = "WM.CombatEffect.Flash";
		private const string SCREEN_SHAKE_KEY = "WM.CombatEffect.ScreenShake";

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
	}
}
