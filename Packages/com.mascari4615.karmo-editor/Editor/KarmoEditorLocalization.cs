using UnityEditor;
using UnityEngine;

namespace KarmoLab.KarmoEditor
{
	/// <summary>
	/// 패키지 내 언어 설정을 관리하고 텍스트를 제공하는 클래스
	/// </summary>
	public static class KarmoEditorLocalization
	{
		private const string LangPrefsKey = "KarmoEditor.Language";

		public enum Language
		{
			Korean,
			English
		}

		public static Language CurrentLanguage
		{
			get => (Language)EditorPrefs.GetInt(LangPrefsKey, (int)Language.Korean);
			set => EditorPrefs.SetInt(LangPrefsKey, (int)value);
		}

		public static string Get(string kr, string en)
		{
			return CurrentLanguage == Language.Korean ? kr : en;
		}

		// 공통 UI 텍스트 예시
		public static string WelcomeTitle => Get("반가워요! KarmoEditor 패키지입니다. 🐾", "Hello! Welcome to KarmoEditor Package. 🐾");
		public static string SettingsCreated => Get("설정 에셋이 생성되었습니다.", "Settings assets created successfully.");
		public static string MutexKilled => Get("뮤텍스가 종료되었습니다.", "Mutex killed successfully.");
	}
}
