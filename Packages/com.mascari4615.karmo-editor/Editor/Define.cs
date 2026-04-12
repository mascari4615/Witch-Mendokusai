namespace KarmoLab.KarmoEditor
{
	/// <summary>
	/// KarmoEditor 패키지 공통 상수 정의
	/// </summary>
	public static class Define
	{
		// Menu Paths
		public const string RootMenu = "KarmoLab/KarmoEditor/";
		public const string DebugMenu = RootMenu + "DEBUG/";
		public const string SettingsMenu = RootMenu + "Settings/";

		public const string LogPrefix = "[KarmoEditor]";

		// Menu Item Priorities
		public const int DefaultPriority = 100;
		public const int DebugPriority = 1000;

		// Asset Creation Paths
		public const string CreateAssetMenuRoot = "KarmoLab/KarmoEditor/";
		public const string CreateAssetMenuSettings = CreateAssetMenuRoot + "Settings";

		// Physical Save Paths
		public const string DefaultSettingsPath = "Assets/Settings/KarmoLab/KarmoEditor";

		// Project Settings Paths
		public const string ProjectSettingsPath = "Project/KarmoLab";
	}
}
