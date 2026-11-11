using System;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DungeonEntranceTheme
	{
		Gosu = 0,
		NightGlass = 1,
		Cut = 2,
	}

	/// <summary>
	/// 던전 입장 화면 스타일 선택. 값은 PlayerPrefs, 모양은 테마별 USS
	/// </summary>
	public static class DungeonEntranceThemes
	{
		private const string PREFS_KEY = "WM.DungeonEntranceTheme";

		public static event Action OnChanged = delegate { };

		public static DungeonEntranceTheme Current => (DungeonEntranceTheme)PlayerPrefs.GetInt(PREFS_KEY, (int)DungeonEntranceTheme.NightGlass);

		public static void Set(DungeonEntranceTheme theme)
		{
			PlayerPrefs.SetInt(PREFS_KEY, (int)theme);
			OnChanged();
		}

		public static string GetClassSuffix(DungeonEntranceTheme theme) => theme switch
		{
			DungeonEntranceTheme.NightGlass => "--night",
			DungeonEntranceTheme.Cut => "--cut",
			_ => "--gosu",
		};

#if UNITY_EDITOR
		[UnityEditor.MenuItem("WM/UI/Dungeon Entrance Theme/Gosu")]
		private static void SelectGosu() => Set(DungeonEntranceTheme.Gosu);

		[UnityEditor.MenuItem("WM/UI/Dungeon Entrance Theme/Night Glass")]
		private static void SelectNightGlass() => Set(DungeonEntranceTheme.NightGlass);

		[UnityEditor.MenuItem("WM/UI/Dungeon Entrance Theme/Cut")]
		private static void SelectCut() => Set(DungeonEntranceTheme.Cut);
#endif
	}
}
