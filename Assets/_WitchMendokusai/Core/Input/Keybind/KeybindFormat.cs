namespace WitchMendokusai
{
	/// <summary>
	/// 입력 경로를 사람이 읽는 글자로. 안내 창, 전체 메뉴, 설정창 키 탭이 같이 씀
	/// "<Keyboard>/j" -> "J", "<Mouse>/leftButton" -> "Mouse Left", "<Keyboard>/leftShift" -> "Left Shift"
	/// </summary>
	public static class KeybindFormat
	{
		public const string NONE_TEXT = "(없음)";

		public static string FormatPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return NONE_TEXT;

			int slashIndex = path.IndexOf('/');
			if (slashIndex < 0 || slashIndex == path.Length - 1)
				return path;

			string device = path.Substring(0, slashIndex).Trim('<', '>');
			string key = path.Substring(slashIndex + 1);

			if (device == "Keyboard")
				return PrettifyKeyboardKey(key);
			if (device == "Mouse")
				return $"Mouse {PrettifyMouseKey(key)}";

			return $"{device} {key}";
		}

		private static string PrettifyKeyboardKey(string key)
		{
			return key switch
			{
				"leftShift" => "Left Shift",
				"rightShift" => "Right Shift",
				"leftCtrl" => "Left Ctrl",
				"rightCtrl" => "Right Ctrl",
				"ctrl" => "Ctrl",
				"leftAlt" => "Left Alt",
				"rightAlt" => "Right Alt",
				"upArrow" => "↑",
				"downArrow" => "↓",
				"leftArrow" => "←",
				"rightArrow" => "→",
				"slash" => "/",
				"space" => "Space",
				"tab" => "Tab",
				_ => key.Length == 1 ? key.ToUpper() : key
			};
		}

		private static string PrettifyMouseKey(string key)
		{
			return key switch
			{
				"leftButton" => "Left",
				"rightButton" => "Right",
				"middleButton" => "Middle",
				"scroll" => "Scroll",
				_ => key
			};
		}
	}
}
